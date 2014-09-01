namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;
    using DefaultMetadataType = System.Collections.Generic.IDictionary<string, object>;

    public abstract class ExportProvider : IDisposableObservable
    {
        internal static readonly ExportDefinition ExportProviderExportDefinition = new ExportDefinition(
            ContractNameServices.GetTypeIdentity(typeof(ExportProvider)),
            PartCreationPolicyConstraint.GetExportMetadata(CreationPolicy.Shared).AddRange(ExportTypeIdentityConstraint.GetExportMetadata(typeof(ExportProvider))));

        internal static readonly ComposablePartDefinition ExportProviderPartDefinition = new ComposablePartDefinition(
            TypeRef.Get(typeof(ExportProviderAsExport)),
            ImmutableDictionary<string, object>.Empty,
            new[] { ExportProviderExportDefinition },
            ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Empty,
            ImmutableList<ImportDefinitionBinding>.Empty,
            string.Empty,
            default(MethodRef),
            null,
            CreationPolicy.Shared,
            true);

        protected static readonly Lazy<object> NotInstantiablePartLazy = new Lazy<object>(() => CannotInstantiatePartWithNoImportingConstructor());

        protected static readonly Type[] EmptyTypeArray = new Type[0];

        protected static readonly object[] EmptyObjectArray = EmptyTypeArray; // Covariance allows us to reuse the derived type empty array.

        /// <summary>
        /// A metadata template used by the generated code.
        /// </summary>
        protected static readonly ImmutableDictionary<string, object> EmptyMetadata = ImmutableDictionary.Create<string, object>();

        /// <summary>
        /// A cache for the <see cref="GetMetadataViewProvider"/> method which has shown up on perf traces.
        /// </summary>
        /// <remarks>
        /// All access to this dictionary is guarded by a lock on this field.
        /// </remarks>
        private Dictionary<Type, IMetadataViewProvider> typeAndSelectedMetadataViewProviderCache = new Dictionary<Type, IMetadataViewProvider>();

        /// <summary>
        /// The metadata view providers available to this ExportProvider.
        /// </summary>
        /// <remarks>
        /// This field is lazy to avoid a chicken-and-egg problem with initializing it in our constructor.
        /// </remarks>
        private readonly Lazy<ImmutableArray<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>> metadataViewProviders;

        private readonly object syncObject = new object();

        /// <summary>
        /// A map of shared boundary names to their shared instances.
        /// The value is a dictionary of types to their Lazy{T} factories.
        /// </summary>
        private readonly ImmutableDictionary<string, Dictionary<TypeRef, object>> sharedInstantiatedExports = ImmutableDictionary.Create<string, Dictionary<TypeRef, object>>();

        /// <summary>
        /// The disposable objects whose lifetimes are shared and tied to a specific sharing boundary.
        /// </summary>
        private readonly ImmutableDictionary<string, HashSet<IDisposable>> disposableInstantiatedSharedParts = ImmutableDictionary.Create<string, HashSet<IDisposable>>();

        /// <summary>
        /// The dispoable objects whose lifetimes are controlled by this instance.
        /// </summary>
        /// <remarks>
        /// Access to this collection is guarded by locking the collection instance itself.
        /// </remarks>
        private readonly HashSet<IDisposable> disposableNonSharedParts = new HashSet<IDisposable>();

        /// <summary>
        /// The sharing boundaries that this ExportProvider creates new sharing boundaries for.
        /// </summary>
        private readonly ImmutableHashSet<string> freshSharingBoundaries = ImmutableHashSet.Create<string>();

        private bool isDisposed;

        protected ExportProvider(ExportProvider parent, IReadOnlyCollection<string> freshSharingBoundaries)
        {
            if (parent == null)
            {
                this.sharedInstantiatedExports = this.sharedInstantiatedExports.Add(string.Empty, new Dictionary<TypeRef, object>());
                this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.Add(string.Empty, new HashSet<IDisposable>());
                this.freshSharingBoundaries = this.freshSharingBoundaries.Add(string.Empty);
            }
            else
            {
                this.sharedInstantiatedExports = parent.sharedInstantiatedExports;
                this.disposableInstantiatedSharedParts = parent.disposableInstantiatedSharedParts;
            }

            if (freshSharingBoundaries != null)
            {
                this.freshSharingBoundaries = this.freshSharingBoundaries.Union(freshSharingBoundaries);
                foreach (string freshSharingBoundary in freshSharingBoundaries)
                {
                    this.sharedInstantiatedExports = this.sharedInstantiatedExports.SetItem(freshSharingBoundary, new Dictionary<TypeRef, object>());
                    this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.SetItem(freshSharingBoundary, new HashSet<IDisposable>());
                }
            }

            var nonDisposableWrapper = (this as ExportProviderAsExport) ?? new ExportProviderAsExport(this);
            this.NonDisposableWrapper = LazyServices.FromValue<object>(nonDisposableWrapper);
            this.NonDisposableWrapperExportAsListOfOne = ImmutableList.Create(
                new Export(ExportProviderExportDefinition, this.NonDisposableWrapper));
            this.metadataViewProviders = parent != null
                ? parent.metadataViewProviders
                : new Lazy<ImmutableArray<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>>(
                    this.GetMetadataViewProviderExtensions);
        }

        bool IDisposableObservable.IsDisposed
        {
            get { return this.isDisposed; }
        }

        /// <summary>
        /// Gets a lazy that creates an instance of DelegatingExportProvider.
        /// </summary>
        protected Lazy<object> NonDisposableWrapper { get; private set; }

        protected ImmutableList<Export> NonDisposableWrapperExportAsListOfOne { get; private set; }

        public Lazy<T> GetExport<T>()
        {
            return this.GetExport<T>(null);
        }

        public Lazy<T> GetExport<T>(string contractName)
        {
            return this.GetExport<T, DefaultMetadataType>(contractName);
        }

        public Lazy<T, TMetadataView> GetExport<T, TMetadataView>()
        {
            return this.GetExport<T, TMetadataView>(null);
        }

        public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
        {
            return this.GetExports<T, TMetadataView>(contractName, ImportCardinality.ExactlyOne).Single();
        }

        public T GetExportedValue<T>()
        {
            return this.GetExport<T>().Value;
        }

        public T GetExportedValue<T>(string contractName)
        {
            return this.GetExport<T>(contractName).Value;
        }

        public IEnumerable<Lazy<T>> GetExports<T>()
        {
            return this.GetExports<T>(null);
        }

        public IEnumerable<Lazy<T>> GetExports<T>(string contractName)
        {
            return this.GetExports<T, DefaultMetadataType>(contractName);
        }

        public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>()
        {
            return this.GetExports<T, TMetadataView>(null);
        }

        public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
        {
            return this.GetExports<T, TMetadataView>(contractName, ImportCardinality.ZeroOrMore);
        }

        public IEnumerable<T> GetExportedValues<T>()
        {
            return this.GetExports<T>().Select(l => l.Value);
        }

        public IEnumerable<T> GetExportedValues<T>(string contractName)
        {
            return this.GetExports<T>(contractName).Select(l => l.Value);
        }

        public virtual IEnumerable<Export> GetExports(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            if (importDefinition.ContractName == ExportProviderExportDefinition.ContractName)
            {
                return this.NonDisposableWrapperExportAsListOfOne;
            }

            bool isExportFactory = importDefinition.ContractName == CompositionConstants.PartCreatorContractName;
            ImportDefinition exportFactoryImportDefinition = null;
            if (isExportFactory)
            {
                // This is a runtime request for an ExportFactory<T>. This can happen for example when an object
                // is supplied to a MEFv1 CompositionContainer's SatisfyImportsOnce method when that object
                // has an importing member of type ExportFactory<T>.
                // We must unwrap the nested import definition to unveil the actual export to be created
                // by this export factory.
                exportFactoryImportDefinition = importDefinition;
                importDefinition = (ImportDefinition)importDefinition.Metadata[CompositionConstants.ExportFactoryProductImportDefinition];
            }

            IEnumerable<ExportInfo> exportInfos = this.GetExportsCore(importDefinition);

            string genericTypeDefinitionContractName;
            Type[] genericTypeArguments;
            if (ComposableCatalog.TryGetOpenGenericExport(importDefinition, out genericTypeDefinitionContractName, out genericTypeArguments))
            {
                var genericTypeImportDefinition = new ImportDefinition(genericTypeDefinitionContractName, importDefinition.Cardinality, importDefinition.Metadata, importDefinition.ExportConstraints);
                var openGenericExports = this.GetExportsCore(genericTypeImportDefinition);
                var closedGenericExports = openGenericExports.Select(export => export.CloseGenericExport(genericTypeArguments));
                exportInfos = exportInfos.Concat(closedGenericExports);
            }

            var filteredExportInfos = from export in exportInfos
                                      where importDefinition.ExportConstraints.All(c => c.IsSatisfiedBy(export.Definition))
                                      select export;

            IEnumerable<Export> exports;
            if (isExportFactory)
            {
                var exportFactoryType = (Type)exportFactoryImportDefinition.Metadata[CompositionConstants.ExportFactoryTypeMetadataName];
                exports = filteredExportInfos.Select(ei => this.CreateExportFactoryExport(ei, exportFactoryType));
            }
            else
            {
                exports = filteredExportInfos.Select(fe => new Export(fe.Definition, fe.ExportedValueGetter));
            }

            var exportsSnapshot = exports.ToArray(); // avoid repeating all the foregoing work each time this sequence is enumerated.
            if (importDefinition.Cardinality == ImportCardinality.ExactlyOne && exportsSnapshot.Length != 1)
            {
                throw new CompositionFailedException();
            }

            return exportsSnapshot;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.isDisposed = true;

                // Snapshot the contents of the collection within the lock,
                // then dispose of the values outside the lock to avoid
                // executing arbitrary 3rd-party code within our lock.
                List<IDisposable> disposableSnapshot;
                lock (this.disposableNonSharedParts)
                {
                    disposableSnapshot = new List<IDisposable>(this.disposableNonSharedParts);
                    this.disposableNonSharedParts.Clear();
                }

                foreach (var sharingBoundary in this.freshSharingBoundaries)
                {
                    var disposablePartsHashSet = this.disposableInstantiatedSharedParts[sharingBoundary];
                    lock (disposablePartsHashSet)
                    {
                        disposableSnapshot.AddRange(disposablePartsHashSet);
                        disposablePartsHashSet.Clear();
                    }
                }

                foreach (var item in disposableSnapshot)
                {
                    item.Dispose();
                }
            }
        }

        protected static object CannotInstantiatePartWithNoImportingConstructor()
        {
            throw new CompositionFailedException("No importing constructor");
        }

        /// <summary>
        /// When implemented by a derived class, returns an <see cref="IEnumerable{T}"/> of values that
        /// satisfy the contract name of the specified <see cref="ImportDefinition"/>.
        /// </summary>
        /// <remarks>
        /// The derived type is *not* expected to filter the exports based on the import definition constraints.
        /// </remarks>
        protected abstract IEnumerable<ExportInfo> GetExportsCore(ImportDefinition importDefinition);

        protected ExportInfo CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> metadata, TypeRef partTypeRef, Func<ExportProvider, Dictionary<TypeRef, object>, bool, object> valueFactory, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(metadata, "metadata");
            Requires.NotNull(partTypeRef, "partTypeRef");
            Requires.NotNull(valueFactory, "valueFactory");

            var provisionalSharedObjects = new Dictionary<TypeRef, object>();
            Func<object> maybeSharedValueFactory = this.GetOrCreateShareableValue(partTypeRef, valueFactory, provisionalSharedObjects, partSharingBoundary, nonSharedInstanceRequired);
            Func<object> memberValueFactory;
            if (exportingMember == null)
            {
                memberValueFactory = maybeSharedValueFactory;
            }
            else
            {
                memberValueFactory = () => GetValueFromMember(maybeSharedValueFactory(), exportingMember);
            }

            return new ExportInfo(importDefinition.ContractName, metadata, memberValueFactory);
        }

        protected object CreateExportFactory(Type importingSiteElementType, IReadOnlyCollection<string> sharingBoundaries, Func<KeyValuePair<object, IDisposable>> valueFactory, Type exportFactoryType, IReadOnlyDictionary<string, object> exportMetadata)
        {
            Requires.NotNull(importingSiteElementType, "importingSiteElementType");
            Requires.NotNull(sharingBoundaries, "sharingBoundaries");
            Requires.NotNull(valueFactory, "valueFactory");
            Requires.NotNull(exportFactoryType, "exportFactoryType");
            Requires.NotNull(exportMetadata, "exportMetadata");

            // ExportFactory.ctor(Func<Tuple<T, Action>>[, TMetadata])
            Type tupleType;
            using (var typeArgs = ArrayRental<Type>.Get(2))
            {
                typeArgs.Value[0] = importingSiteElementType;
                typeArgs.Value[1] = typeof(Action);
                tupleType = typeof(Tuple<,>).MakeGenericType(typeArgs.Value);
            }

            Func<object> factory = () =>
            {
                KeyValuePair<object, IDisposable> constructedValueAndDisposable = valueFactory();

                using (var ctorArgs = ArrayRental<object>.Get(2))
                {
                    ctorArgs.Value[0] = constructedValueAndDisposable.Key;
                    ctorArgs.Value[1] = constructedValueAndDisposable.Value != null ? new Action(constructedValueAndDisposable.Value.Dispose) : null;
                    return Activator.CreateInstance(tupleType, ctorArgs.Value);
                }
            };

            using (var ctorArgs = ArrayRental<object>.Get(exportFactoryType.GenericTypeArguments.Length))
            {
                ctorArgs.Value[0] = DelegateServices.As(factory, tupleType);
                if (ctorArgs.Value.Length > 1)
                {
                    ctorArgs.Value[1] = this.GetStrongTypedMetadata(exportMetadata, exportFactoryType.GenericTypeArguments[1]);
                }

                var ctor = exportFactoryType.GetConstructors()[0];
                return ctor.Invoke(ctorArgs.Value);
            }
        }

        private Export CreateExportFactoryExport(ExportInfo exportInfo, Type exportFactoryType)
        {
            Requires.NotNull(exportFactoryType, "exportFactoryType");

            var exportFactoryCreator = (Func<object>)(() => this.CreateExportFactory(
                typeof(object),
                ImmutableHashSet<string>.Empty, // no support for sub-scopes queried for imperatively.
                () =>
                {
                    object value = exportInfo.ExportedValueGetter();
                    return new KeyValuePair<object, IDisposable>(value, value as IDisposable);
                },
                exportFactoryType,
                exportInfo.Definition.Metadata));
            var exportFactoryTypeIdentity = ContractNameServices.GetTypeIdentity(exportFactoryType);
            var exportFactoryMetadata = exportInfo.Definition.Metadata.ToImmutableDictionary()
                .SetItem(CompositionConstants.ExportTypeIdentityMetadataName, exportFactoryTypeIdentity)
                .SetItem(CompositionConstants.PartCreationPolicyMetadataName, CreationPolicy.NonShared)
                .SetItem(CompositionConstants.ProductDefinitionMetadataName, exportInfo.Definition);
            return new Export(
                exportFactoryTypeIdentity,
                exportFactoryMetadata,
                exportFactoryCreator);
        }

        protected object GetStrongTypedMetadata(IReadOnlyDictionary<string, object> metadata, Type metadataType)
        {
            Requires.NotNull(metadata, "metadata");
            Requires.NotNull(metadataType, "metadataType");

            var metadataViewProvider = this.GetMetadataViewProvider(metadataType);
            return metadataViewProvider.CreateProxy(
                metadata,
                GetMetadataViewDefaults(metadataType),
                metadataType);
        }

        protected object GetValueFromMember(object exportingPart, ImportDefinitionBinding import, ExportDefinitionBinding export)
        {
            return this.GetValueFromMember(exportingPart, export.ExportingMember, import.ImportingSiteElementType, export.ExportedValueType);
        }

        /// <summary>
        /// Gets the value from some member of a part.
        /// </summary>
        /// <param name="exportingPart">The instance of the part to extract the value from. May be <c>null</c> for static exports.</param>
        /// <param name="exportingMember">The member exporting the value. May be <c>null</c> for exporting the type/instance itself.</param>
        /// <param name="importingSiteElementType">The type of the importing member, with ImportMany collections and Lazy/ExportFactory stripped away.</param>
        /// <param name="exportedValueType">The contractually exported value type.</param>
        /// <returns>The value of the member.</returns>
        protected object GetValueFromMember(object exportingPart, MemberInfo exportingMember, Type importingSiteElementType = null, Type exportedValueType = null)
        {
            Requires.NotNull(exportingMember, "exportingMember");

            if (exportingMember == null)
            {
                return exportingPart;
            }

            var field = exportingMember as FieldInfo;
            if (field != null)
            {
                return field.GetValue(exportingPart);
            }

            var property = exportingMember as PropertyInfo;
            if (property != null)
            {
                return property.GetValue(exportingPart);
            }

            var method = exportingMember as MethodInfo;
            if (method != null)
            {
                // If the method came from a property, return the result of the property getter rather than return the delegate.
                if (method.IsSpecialName && method.GetParameters().Length == 0 && method.Name.StartsWith("get_"))
                {
                    return method.Invoke(exportingPart, EmptyObjectArray);
                }

                Type delegateType = importingSiteElementType != null && typeof(Delegate).GetTypeInfo().IsAssignableFrom(importingSiteElementType.GetTypeInfo())
                    ? importingSiteElementType
                    : (exportedValueType ?? ReflectionHelpers.GetContractTypeForDelegate(method));
                return method.CreateDelegate(delegateType, method.IsStatic ? null : exportingPart);
            }

            throw new NotSupportedException();
        }

        protected Func<object> GetOrCreateShareableValue(TypeRef partTypeRef, Func<ExportProvider, Dictionary<TypeRef, object>, bool, object> valueFactory, Dictionary<TypeRef, object> provisionalSharedObjects, string partSharingBoundary, bool nonSharedInstanceRequired)
        {
            Requires.NotNull(partTypeRef, "partTypeRef");

            if (!nonSharedInstanceRequired)
            {
                object provisionalObject;
                if (this.TryGetProvisionalSharedExport(provisionalSharedObjects, partTypeRef, out provisionalObject))
                {
                    return DelegateServices.FromValue(provisionalObject);
                }

                Lazy<object> lazyResult;
                if (this.TryGetSharedInstanceFactory(partSharingBoundary, partTypeRef, out lazyResult))
                {
                    return lazyResult.AsFunc();
                }
            }

            Func<object> result = () => valueFactory(this, provisionalSharedObjects, nonSharedInstanceRequired);

            if (!nonSharedInstanceRequired)
            {
                var lazyResult = new Lazy<object>(result);
                lazyResult = this.GetOrAddSharedInstanceFactory(partSharingBoundary, partTypeRef, lazyResult);
                result = lazyResult.AsFunc();
            }

            return result;
        }

        private bool TryGetSharedInstanceFactory<T>(string partSharingBoundary, TypeRef partTypeRef, out Lazy<T> value)
        {
            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                object valueObject;
                bool result = sharingBoundary.TryGetValue(partTypeRef, out valueObject);
                value = (Lazy<T>)valueObject;
                return result;
            }
        }

        private Lazy<object> GetOrAddSharedInstanceFactory(string partSharingBoundary, TypeRef partTypeRef, Lazy<object> value)
        {
            Requires.NotNull(partTypeRef, "partTypeRef");
            Requires.NotNull(value, "value");

            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                object priorValue;
                if (sharingBoundary.TryGetValue(partTypeRef, out priorValue))
                {
                    return (Lazy<object>)priorValue;
                }

                sharingBoundary.Add(partTypeRef, value);
                return value;
            }
        }

        /// <summary>
        /// Adds a value to be disposed of when this or a parent ExportProvider is disposed of.
        /// </summary>
        /// <param name="instantiatedPart">The part to be disposed.</param>
        /// <param name="sharingBoundary">
        /// The sharing boundary associated with the part.
        /// May be null for non-shared parts, or the empty string for the default sharing scope.
        /// </param>
        protected void TrackDisposableValue(IDisposable instantiatedPart, string sharingBoundary)
        {
            Requires.NotNull(instantiatedPart, "instantiatedPart");

            if (sharingBoundary == null)
            {
                lock (this.disposableNonSharedParts)
                {
                    this.disposableNonSharedParts.Add(instantiatedPart);
                }
            }
            else
            {
                var disposablePartsHashSet = this.disposableInstantiatedSharedParts[sharingBoundary];
                lock (disposablePartsHashSet)
                {
                    disposablePartsHashSet.Add(instantiatedPart);
                }
            }
        }

        protected MethodInfo GetMethodWithArity(Type declaringType, string methodName, int arity)
        {
            return declaringType.GetTypeInfo().GetDeclaredMethods(methodName)
                .Single(m => m.GetGenericArguments().Length == arity);
        }

        protected internal interface IMetadataDictionary : IDictionary<string, object>, IReadOnlyDictionary<string, object> { }

        private static readonly Dictionary<Type, IReadOnlyDictionary<string, object>> GetMetadataViewDefaultsCache = new Dictionary<Type, IReadOnlyDictionary<string, object>>();

        /// <summary>
        /// Gets a dictionary of metadata that describes all the default values supplied by a metadata view.
        /// </summary>
        /// <param name="metadataView">The metadata view type.</param>
        /// <returns>A dictionary of default metadata values.</returns>
        protected static IReadOnlyDictionary<string, object> GetMetadataViewDefaults(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            IReadOnlyDictionary<string, object> result;
            lock (GetMetadataViewDefaultsCache)
            {
                GetMetadataViewDefaultsCache.TryGetValue(metadataView, out result);
            }

            if (result == null)
            {
                if (metadataView.GetTypeInfo().IsInterface && !metadataView.Equals(typeof(IDictionary<string, object>)))
                {
                    var metadataBuilder = ImmutableDictionary.CreateBuilder<string, object>();
                    foreach (var property in metadataView.EnumProperties().WherePublicInstance())
                    {
                        if (!metadataBuilder.ContainsKey(property.Name))
                        {
                            var defaultValueAttribute = property.GetCustomAttributesCached<DefaultValueAttribute>().FirstOrDefault();
                            if (defaultValueAttribute != null)
                            {
                                metadataBuilder.Add(property.Name, defaultValueAttribute.Value);
                            }
                        }
                    }

                    result = metadataBuilder.ToImmutable();
                }
                else
                {
                    result = ImmutableDictionary<string, object>.Empty;
                }

                lock (GetMetadataViewDefaultsCache)
                {
                    GetMetadataViewDefaultsCache[metadataView] = result;
                }
            }

            return result;
        }

        protected internal static int GetOrderMetadata(IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(metadata, "metadata");

            object value = metadata.GetValueOrDefault("OrderPrecedence");
            return value is int ? (int)value : 0;
        }

        private bool TryGetProvisionalSharedExport(IReadOnlyDictionary<TypeRef, object> provisionalSharedObjects, TypeRef partTypeRef, out object value)
        {
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");
            Requires.NotNull(partTypeRef, "partTypeRef");

            lock (provisionalSharedObjects)
            {
                return provisionalSharedObjects.TryGetValue(partTypeRef, out value);
            }
        }

        private IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName, ImportCardinality cardinality)
        {
            Verify.NotDisposed(this);
            contractName = string.IsNullOrEmpty(contractName) ? ContractNameServices.GetTypeIdentity(typeof(T)) : contractName;
            IMetadataViewProvider metadataViewProvider = GetMetadataViewProvider(typeof(TMetadataView));

            var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty
                .Union(PartDiscovery.GetExportTypeIdentityConstraints(typeof(T)));

            if (typeof(TMetadataView) != typeof(DefaultMetadataType))
            {
                constraints = constraints.Add(ImportMetadataViewConstraint.GetConstraint(TypeRef.Get(typeof(TMetadataView))));
            }

            var importMetadata = PartDiscovery.GetImportMetadataForGenericTypeImport(typeof(T));
            var importDefinition = new ImportDefinition(contractName, cardinality, importMetadata, constraints);
            IEnumerable<Export> results = this.GetExports(importDefinition);

            return results.Select(result => new Lazy<T, TMetadataView>(
                () => (T)result.Value,
                (TMetadataView)metadataViewProvider.CreateProxy(
                    result.Metadata,
                    GetMetadataViewDefaults(typeof(TMetadataView)),
                    typeof(TMetadataView))))
                .ToImmutableHashSet();
        }

        private ImmutableArray<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>> GetMetadataViewProviderExtensions()
        {
            var importDefinition = new ImportDefinition(
                ContractNameServices.GetTypeIdentity(typeof(IMetadataViewProvider)),
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary<string, object>.Empty,
                ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty);
            var extensions = from export in this.GetExports(importDefinition)
                             orderby GetOrderMetadata(export.Metadata) descending
                             select new Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>(() => (IMetadataViewProvider)export.Value, export.Metadata);
            var result = ImmutableArray.CreateRange(extensions);
            return result;
        }

        /// <summary>
        /// Gets a provider that can create a metadata view of a specified type over a dictionary of metadata.
        /// </summary>
        /// <param name="metadataView">The type of metadata view required.</param>
        /// <returns>A metadata view provider.</returns>
        /// <exception cref="NotSupportedException">Thrown if no metadata view provider available is compatible with the type.</exception>
        internal virtual IMetadataViewProvider GetMetadataViewProvider(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            IMetadataViewProvider metadataViewProvider;
            lock (this.typeAndSelectedMetadataViewProviderCache)
            {
                this.typeAndSelectedMetadataViewProviderCache.TryGetValue(metadataView, out metadataViewProvider);
            }

            if (metadataViewProvider == null)
            {
                foreach (var viewProvider in this.metadataViewProviders.Value)
                {
                    if (viewProvider.Value.IsMetadataViewSupported(metadataView))
                    {
                        metadataViewProvider = viewProvider.Value;
                        break;
                    }
                }

                if (metadataViewProvider == null)
                {
                    throw new NotSupportedException("Type of metadata view is unsupported.");
                }

                lock (this.typeAndSelectedMetadataViewProviderCache)
                {
                    this.typeAndSelectedMetadataViewProviderCache[metadataView] = metadataViewProvider;
                }
            }

            return metadataViewProvider;
        }

        private Dictionary<TypeRef, object> AcquireSharingBoundaryInstances(string sharingBoundaryName)
        {
            Requires.NotNull(sharingBoundaryName, "sharingBoundaryName");

            var sharingBoundary = this.sharedInstantiatedExports.GetValueOrDefault(sharingBoundaryName);
            if (sharingBoundary == null)
            {
                // This means someone is trying to create a part
                // that belongs to a sharing boundary that has not yet been created.
                throw new CompositionFailedException("Inappropriate request for export from part that belongs to another sharing boundary.");
            }

            return sharingBoundary;
        }

        protected struct ExportInfo
        {
            public ExportInfo(string contractName, IReadOnlyDictionary<string, object> metadata, Func<object> exportedValueGetter)
                : this(new ExportDefinition(contractName, metadata), exportedValueGetter)
            {
            }

            public ExportInfo(ExportDefinition exportDefinition, Func<object> exportedValueGetter)
                : this()
            {
                Requires.NotNull(exportDefinition, "exportDefinition");
                Requires.NotNull(exportedValueGetter, "exportedValueGetter");

                this.Definition = exportDefinition;
                this.ExportedValueGetter = exportedValueGetter;
            }

            public ExportDefinition Definition { get; private set; }

            public Func<object> ExportedValueGetter { get; private set; }

            internal ExportInfo CloseGenericExport(Type[] genericTypeArguments)
            {
                Requires.NotNull(genericTypeArguments, "genericTypeArguments");

                string openGenericExportTypeIdentity = (string)this.Definition.Metadata[CompositionConstants.ExportTypeIdentityMetadataName];
                string genericTypeDefinitionIdentityPattern = openGenericExportTypeIdentity;
                string[] genericTypeArgumentIdentities = genericTypeArguments.Select(ContractNameServices.GetTypeIdentity).ToArray();
                string closedTypeIdentity = string.Format(CultureInfo.InvariantCulture, genericTypeDefinitionIdentityPattern, genericTypeArgumentIdentities);
                var metadata = ImmutableDictionary.CreateRange(this.Definition.Metadata).SetItem(CompositionConstants.ExportTypeIdentityMetadataName, closedTypeIdentity);

                string contractName = this.Definition.ContractName == openGenericExportTypeIdentity
                    ? closedTypeIdentity : this.Definition.ContractName;

                return new ExportInfo(contractName, metadata, this.ExportedValueGetter);
            }
        }

        private class ExportProviderAsExport : DelegatingExportProvider
        {
            internal ExportProviderAsExport(ExportProvider inner)
                : base(inner)
            {
            }

            protected override void Dispose(bool disposing)
            {
                throw new InvalidOperationException("This instance is an import and cannot be directly disposed.");
            }
        }
    }
}
