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
    using System.Runtime.ExceptionServices;
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
            ImmutableDictionary<string, object>.Empty.Add(CompositionConstants.DgmlCategoryPartMetadataName, new[] { "VsMEFBuiltIn" }),
            new[] { ExportProviderExportDefinition },
            ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Empty,
            ImmutableList<ImportDefinitionBinding>.Empty,
            string.Empty,
            default(MethodRef),
            default(ConstructorRef),
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
        /// The value is a dictionary of types to their lazily-constructed instances and state.
        /// </summary>
        private readonly ImmutableDictionary<string, Dictionary<TypeRef, PartLifecycleTracker>> sharedInstantiatedParts = ImmutableDictionary.Create<string, Dictionary<TypeRef, PartLifecycleTracker>>();

        /// <summary>
        /// A map of sharing boundary names to the ExportProvider that owns them.
        /// </summary>
        private readonly ImmutableDictionary<string, ExportProvider> sharingBoundaryExportProviderOwners = ImmutableDictionary.Create<string, ExportProvider>();

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
                this.sharedInstantiatedParts = this.sharedInstantiatedParts.Add(string.Empty, new Dictionary<TypeRef, PartLifecycleTracker>());
                this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.Add(string.Empty, new HashSet<IDisposable>());
                this.freshSharingBoundaries = this.freshSharingBoundaries.Add(string.Empty);
            }
            else
            {
                this.sharingBoundaryExportProviderOwners = parent.sharingBoundaryExportProviderOwners;
                this.sharedInstantiatedParts = parent.sharedInstantiatedParts;
                this.disposableInstantiatedSharedParts = parent.disposableInstantiatedSharedParts;
            }

            if (freshSharingBoundaries != null)
            {
                this.freshSharingBoundaries = this.freshSharingBoundaries.Union(freshSharingBoundaries);
                foreach (string freshSharingBoundary in freshSharingBoundaries)
                {
                    this.sharedInstantiatedParts = this.sharedInstantiatedParts.SetItem(freshSharingBoundary, new Dictionary<TypeRef, PartLifecycleTracker>());
                    this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.SetItem(freshSharingBoundary, new HashSet<IDisposable>());
                }
            }

            this.sharingBoundaryExportProviderOwners = this.sharingBoundaryExportProviderOwners.SetItems(
                this.freshSharingBoundaries.Select(boundary => new KeyValuePair<string, ExportProvider>(boundary, this)));

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

        protected ExportInfo CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> exportMetadata, TypeRef partTypeRef, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(exportMetadata, "metadata");
            Requires.NotNull(partTypeRef, "partTypeRef");

            PartLifecycleTracker maybeSharedValueFactory = this.GetOrCreateShareableValue(partTypeRef, partSharingBoundary, importDefinition.Metadata, nonSharedInstanceRequired);
            Func<object> memberValueFactory;
            if (exportingMember == null)
            {
                memberValueFactory = maybeSharedValueFactory.GetValueReadyToExpose;
            }
            else
            {
                memberValueFactory = () => GetValueFromMember(maybeSharedValueFactory.GetValueReadyToRetrieveExportingMembers(), exportingMember);
            }

            return new ExportInfo(importDefinition.ContractName, exportMetadata, memberValueFactory);
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

        /// <summary>
        /// Gets the value from some member of a part.
        /// </summary>
        /// <param name="exportingPart">The instance of the part to extract the value from. May be <c>null</c> for static exports.</param>
        /// <param name="exportingMember">The member exporting the value. May be <c>null</c> for exporting the type/instance itself.</param>
        /// <param name="importingSiteElementType">The type of the importing member, with ImportMany collections and Lazy/ExportFactory stripped away.</param>
        /// <param name="exportedValueType">The contractually exported value type.</param>
        /// <returns>The value of the member.</returns>
        protected static object GetValueFromMember(object exportingPart, MemberInfo exportingMember, Type importingSiteElementType = null, Type exportedValueType = null)
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

                object target = method.IsStatic ? null : exportingPart;
                if (importingSiteElementType != null)
                {
                    Type delegateType = typeof(Delegate).GetTypeInfo().IsAssignableFrom(importingSiteElementType.GetTypeInfo())
                        ? importingSiteElementType
                        : (exportedValueType ?? ReflectionHelpers.GetContractTypeForDelegate(method));
                    return method.CreateDelegate(delegateType, target);
                }
                else
                {
                    return new ExportedDelegate(target, method);
                }
            }

            throw new NotSupportedException();
        }

        protected PartLifecycleTracker GetOrCreateShareableValue(TypeRef partTypeRef, string partSharingBoundary, IReadOnlyDictionary<string, object> importMetadata, bool nonSharedInstanceRequired)
        {
            Requires.NotNull(partTypeRef, "partTypeRef");

            if (!nonSharedInstanceRequired)
            {
                PartLifecycleTracker existingLifecycle;
                if (this.TryGetSharedInstanceFactory(partSharingBoundary, partTypeRef, out existingLifecycle))
                {
                    return existingLifecycle;
                }
            }

            // Be careful to pass the export provider that owns the sharing boundary for this part into the value factory.
            // If we accidentally capture "this", then if this is a sub-scope ExportProvider and we're constructing
            // a parent scope shared part, then we tie the lifetime of this child scope to the lifetime of the 
            // parent scoped part's value factory. If it never evaluates, we never get released even after our own disposal.
            ExportProvider owningExportProvider = partSharingBoundary != null ? this.sharingBoundaryExportProviderOwners[partSharingBoundary] : this;
            var partLifecycle = owningExportProvider.CreatePartLifecycleTracker(partTypeRef, importMetadata);

            if (!nonSharedInstanceRequired)
            {
                // Since we have not been holding a lock, we must now reconcile the creation of this
                // shared instance with a dictionary of shared instances to make sure there is only one that survives.
                partLifecycle = this.GetOrAddSharedInstanceFactory(partSharingBoundary, partTypeRef, partLifecycle);
            }

            return partLifecycle;
        }

        protected internal abstract PartLifecycleTracker CreatePartLifecycleTracker(TypeRef partType, IReadOnlyDictionary<string, object> importMetadata);

        private bool TryGetSharedInstanceFactory(string partSharingBoundary, TypeRef partTypeRef, out PartLifecycleTracker value)
        {
            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                bool result = sharingBoundary.TryGetValue(partTypeRef, out value);
                return result;
            }
        }

        private PartLifecycleTracker GetOrAddSharedInstanceFactory(string partSharingBoundary, TypeRef partTypeRef, PartLifecycleTracker value)
        {
            Requires.NotNull(partTypeRef, "partTypeRef");
            Requires.NotNull(value, "value");

            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                PartLifecycleTracker priorValue;
                if (sharingBoundary.TryGetValue(partTypeRef, out priorValue))
                {
                    return priorValue;
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
                            var defaultValueAttribute = property.GetFirstAttribute<DefaultValueAttribute>();
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

        private static T CastValueTo<T>(object value)
        {
            if (value is ExportedDelegate && typeof(Delegate).IsAssignableFrom(typeof(T)))
            {
                var exportedDelegate = (ExportedDelegate)value;
                return (T)(object)exportedDelegate.CreateDelegate(typeof(T));
            }
            else
            {
                return (T)value;
            }
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
                () => CastValueTo<T>(result.Value),
                (TMetadataView)metadataViewProvider.CreateProxy(
                    result.Metadata,
                    GetMetadataViewDefaults(typeof(TMetadataView)),
                    typeof(TMetadataView))))
                .ToArray();
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

        private Dictionary<TypeRef, PartLifecycleTracker> AcquireSharingBoundaryInstances(string sharingBoundaryName)
        {
            Requires.NotNull(sharingBoundaryName, "sharingBoundaryName");

            var sharingBoundary = this.sharedInstantiatedParts.GetValueOrDefault(sharingBoundaryName);
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

        protected internal abstract class PartLifecycleTracker : IDisposable
        {
            private readonly int ownerThreadId;
            private readonly string sharingBoundary;
            private readonly HashSet<PartLifecycleTracker> importedParts;
            private Exception fault;

            public PartLifecycleTracker(ExportProvider owningExportProvider, string sharingBoundary)
            {
                Requires.NotNull(owningExportProvider, "owningExportProvider");

                this.ownerThreadId = Thread.CurrentThread.ManagedThreadId;
                this.OwningExportProvider = owningExportProvider;
                this.sharingBoundary = sharingBoundary;
                this.importedParts = new HashSet<PartLifecycleTracker>();
                this.State = PartLifecycleState.NotCreated;
            }

            public object Value { get; private set; }

            public PartLifecycleState State { get; private set; }

            protected ExportProvider OwningExportProvider { get; private set; }

            private bool IsOwnedByThisThread
            {
                get { return Thread.CurrentThread.ManagedThreadId == this.ownerThreadId; }
            }

            public void Create()
            {
                lock (this)
                {
                    this.VerifyState(PartLifecycleState.NotCreated);
                    try
                    {
                        this.Value = this.CreateValue();
                        if (this.Value is IDisposable)
                        {
                            this.OwningExportProvider.TrackDisposableValue(this, this.sharingBoundary);
                        }

                        this.UpdateState(PartLifecycleState.Created);
                    }
                    catch (Exception ex)
                    {
                        this.Fault(ex);
                        throw;
                    }
                }
            }

            public void SatisfyImmediateImports()
            {
                lock (this)
                {
                    this.VerifyState(PartLifecycleState.Created);
                    try
                    {
                        this.SatisfyImports();
                        this.UpdateState(PartLifecycleState.ImmediateImportsSatisfied);
                    }
                    catch (Exception ex)
                    {
                        this.Fault(ex);
                        throw;
                    }
                }
            }

            private void WaitForState(PartLifecycleState requiredState)
            {
                lock (this)
                {
                    this.ThrowIfFaulted();

                    while (this.State < requiredState)
                    {
                        Monitor.Wait(this);
                        this.ThrowIfFaulted();
                    }
                }
            }

            private void MoveToState(PartLifecycleState requiredState)
            {
                lock (this)
                {
                    this.ThrowIfFaulted();

                    while (this.State < requiredState)
                    {
                        this.MoveNext();
                        this.ThrowIfFaulted();
                    }
                }
            }

            private void AdvanceToState(PartLifecycleState requiredState)
            {
                if (this.IsOwnedByThisThread)
                {
                    this.MoveToState(requiredState);
                }
                else
                {
                    this.WaitForState(requiredState);
                }
            }

            // TODO: this method should be called at the bottom of a callstack that returns MEF exports,
            // on each of the exports.
            // It should also be called on each export before being passed to an importing constructor (to match MEFv1 behavior).
            // It should also be called when any Lazy<> that is set to an importing property or passed to an importing constructor is evaluated.
            // Consider how it can detect a circular dependency and throw appropriately rather than StackOverflow or deadlock.
            public object GetValueReadyToExpose()
            {
                this.AdvanceToState(PartLifecycleState.Final);
                return this.Value;
            }

            public object GetValueReadyToRetrieveExportingMembers()
            {
                this.AdvanceToState(PartLifecycleState.Created);
                return this.Value;
            }

            public void NotifyTransitiveImportsSatisfied()
            {
                lock (this)
                {
                    this.VerifyState(PartLifecycleState.ImmediateImportsSatisfiedTransitively);
                    try
                    {
                        this.InvokeOnImportsSatisfied();
                        this.UpdateState(PartLifecycleState.OnImportsSatisfiedInvoked);
                    }
                    catch (Exception ex)
                    {
                        this.Fault(ex);
                        throw;
                    }
                }
            }

            protected abstract object CreateValue();

            protected abstract void SatisfyImports();

            protected abstract void InvokeOnImportsSatisfied();

            protected void ReportImportedPart(PartLifecycleTracker importedPart)
            {
                if (importedPart != null)
                {
                    lock (this)
                    {
                        this.importedParts.Add(importedPart);
                    }
                }
            }

            private void AdvanceToStateTransitively(PartLifecycleState requiredState)
            {
                try
                {
                    this.AdvanceToState(requiredState - 1);

                    var transitivelyImportedParts = new HashSet<PartLifecycleTracker>();
                    this.CollectTransitiveCloserOfNonLazyImportedParts(transitivelyImportedParts, requiredState);
                    foreach (var importedPart in transitivelyImportedParts)
                    {
                        if (importedPart != this)
                        {
                            importedPart.AdvanceToState(requiredState - 1);
                        }
                    }

                    // Update everyone involved so they know they're transitively done with this work.
                    foreach (var importedPart in transitivelyImportedParts)
                    {
                        importedPart.UpdateState(requiredState);
                    }
                }
                catch (Exception ex)
                {
                    this.Fault(ex);
                    throw;
                }
            }

            private void CollectTransitiveCloserOfNonLazyImportedParts(HashSet<PartLifecycleTracker> parts, PartLifecycleState excludePartsAfterState)
            {
                Requires.NotNull(parts, "parts");

                lock (this)
                {
                    if (this.State <= excludePartsAfterState && this.State > PartLifecycleState.NotCreated && parts.Add(this))
                    {
                        foreach (var importedPart in this.importedParts)
                        {
                            importedPart.CollectTransitiveCloserOfNonLazyImportedParts(parts, excludePartsAfterState);
                        }
                    }
                }
            }

            private void VerifyState(PartLifecycleState expectedState)
            {
                lock (this)
                {
                    this.ThrowIfFaulted();

                    if (this.State != expectedState)
                    {
                        Verify.FailOperation(Strings.UnexpectedSharedPartState, this.State, PartLifecycleState.Created);
                    }
                }
            }

            private void ThrowIfFaulted()
            {
                if (this.fault != null)
                {
                    ExceptionDispatchInfo.Capture(this.fault).Throw();
                }
            }

            private void UpdateState(PartLifecycleState newState)
            {
                lock (this)
                {
                    if (this.State < newState)
                    {
                        this.State = newState;
                        Monitor.PulseAll(this);
                    }
                }
            }

            private void Fault(Exception exception)
            {
                Assumes.True(Monitor.IsEntered(this));
                Report.If(this.fault != null, "We shouldn't have faulted twice in a row. The first should have done us in.");
                if (exception != null)
                {
                    this.fault = exception;
                    this.Dispose();
                    Monitor.PulseAll(this);
                }
            }

            private void MoveNext()
            {
                lock (this)
                {
                    switch (this.State + 1)
                    {
                        case PartLifecycleState.Created:
                            this.Create();
                            break;
                        case PartLifecycleState.ImmediateImportsSatisfied:
                            this.SatisfyImmediateImports();
                            break;
                        case PartLifecycleState.ImmediateImportsSatisfiedTransitively:
                            this.AdvanceToStateTransitively(PartLifecycleState.ImmediateImportsSatisfiedTransitively);
                            break;
                        case PartLifecycleState.OnImportsSatisfiedInvoked:
                            this.NotifyTransitiveImportsSatisfied();
                            break;
                        case PartLifecycleState.OnImportsSatisfiedInvokedTransitively:
                            this.AdvanceToStateTransitively(PartLifecycleState.OnImportsSatisfiedInvokedTransitively);
                            break;
                        case PartLifecycleState.Final:
                            // Nothing to do here. This state is just a marker.
                            this.UpdateState(PartLifecycleState.Final);
                            break;
                        default:
                            throw Verify.FailOperation("MEF part already in final state.");
                    }
                }
            }

            public void Dispose()
            {
                IDisposable disposableValue = this.Value as IDisposable;
                this.Value = null;
                if (disposableValue != null)
                {
                    disposableValue.Dispose();
                }
            }
        }

        protected internal enum PartLifecycleState
        {
            NotCreated,
            Created,
            ImmediateImportsSatisfied,
            ImmediateImportsSatisfiedTransitively,
            OnImportsSatisfiedInvoked,
            OnImportsSatisfiedInvokedTransitively,
            Final,
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
