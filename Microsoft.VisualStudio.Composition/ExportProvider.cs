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
    using Validation;
    using DefaultMetadataType = System.Collections.Generic.IDictionary<string, object>;

    public abstract class ExportProvider : IDisposableObservable
    {
        internal static readonly ExportDefinition ExportProviderExportDefinition = new ExportDefinition(
            ContractNameServices.GetTypeIdentity(typeof(ExportProvider)),
            PartCreationPolicyConstraint.GetExportMetadata(CreationPolicy.Shared).AddRange(ExportTypeIdentityConstraint.GetExportMetadata(typeof(ExportProvider))));

        internal static readonly ComposablePartDefinition ExportProviderPartDefinition = new ComposablePartDefinition(
            Reflection.TypeRef.Get(typeof(ExportProviderAsExport)),
            new[] { ExportProviderExportDefinition },
            ImmutableDictionary<MemberInfo, IReadOnlyCollection<ExportDefinition>>.Empty,
            ImmutableList<ImportDefinitionBinding>.Empty,
            string.Empty,
            null,
            null,
            CreationPolicy.Shared,
            true);

        protected static readonly LazyPart<object> NotInstantiablePartLazy = new LazyPart<object>(() => CannotInstantiatePartWithNoImportingConstructor());

        protected static readonly Type[] EmptyTypeArray = new Type[0];

        protected static readonly object[] EmptyObjectArray = EmptyTypeArray; // Covariance allows us to reuse the derived type empty array.

        /// <summary>
        /// A metadata template used by the generated code.
        /// </summary>
        protected static readonly ImmutableDictionary<string, object> EmptyMetadata = ImmutableDictionary.Create<string, object>();

        /// <summary>
        /// An array initialized by the generated code derived class that contains the value of 
        /// AssemblyName.FullName for each assembly that must be reflected into.
        /// </summary>
        protected string[] assemblyNames;

        /// <summary>
        /// An array initialized by the generated code derived class that contains the value of 
        /// AssemblyName.CodeBasePath for each assembly that must be reflected into.
        /// </summary>
        protected string[] assemblyCodeBasePaths;

        /// <summary>
        /// An array of manifest modules required for access by reflection.
        /// </summary>
        /// <remarks>
        /// This field is initialized to an array of appropriate size by the derived code-gen'd class.
        /// Its elements are individually lazily initialized.
        /// </remarks>
        protected Module[] cachedManifests;

        /// <summary>
        /// An array of types required for access by reflection.
        /// </summary>
        /// <remarks>
        /// This field is initialized to an array of appropriate size by the derived code-gen'd class.
        /// Its elements are individually lazily initialized.
        /// </remarks>
        protected Type[] cachedTypes;

        /// <summary>
        /// A list of built-in metadata view providers that should be used before trying to get additional ones
        /// from the extensions.
        /// </summary>
        private static readonly ImmutableList<IMetadataViewProvider> BuiltInMetadataViewProviders = ImmutableList.Create(
            PassthroughMetadataViewProvider.Default,
            MetadataViewClassProvider.Default);

        private static readonly IAssemblyLoader BuiltInAssemblyLoader = new AssemblyLoaderByFullName();

        private ThreadLocal<bool> initializingAssemblyLoader = new ThreadLocal<bool>();

        /// <summary>
        /// The metadata view providers available to this ExportProvider.
        /// </summary>
        /// <remarks>
        /// This field is lazy to avoid a chicken-and-egg problem with initializing it in our constructor.
        /// </remarks>
        private readonly Lazy<ImmutableList<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>> metadataViewProviders;

        private readonly Lazy<IAssemblyLoader> assemblyLoadProvider;

        /// <summary>
        /// An array of types 
        /// </summary>
        private List<Reflection.TypeRef> runtimeCreatedTypes;

        private readonly object syncObject = new object();

        /// <summary>
        /// A map of shared boundary names to their shared instances.
        /// The value is a dictionary of types to their Lazy{T} factories.
        /// </summary>
        private readonly ImmutableDictionary<string, Dictionary<int, object>> sharedInstantiatedExports = ImmutableDictionary.Create<string, Dictionary<int, object>>();

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
                this.sharedInstantiatedExports = this.sharedInstantiatedExports.Add(string.Empty, new Dictionary<int, object>());
                this.runtimeCreatedTypes = new List<Reflection.TypeRef>();
                this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.Add(string.Empty, new HashSet<IDisposable>());
                this.freshSharingBoundaries = this.freshSharingBoundaries.Add(string.Empty);
            }
            else
            {
                this.sharedInstantiatedExports = parent.sharedInstantiatedExports;
                this.runtimeCreatedTypes = parent.runtimeCreatedTypes;
                this.disposableInstantiatedSharedParts = parent.disposableInstantiatedSharedParts;
            }

            if (freshSharingBoundaries != null)
            {
                this.freshSharingBoundaries = this.freshSharingBoundaries.Union(freshSharingBoundaries);
                foreach (string freshSharingBoundary in freshSharingBoundaries)
                {
                    this.sharedInstantiatedExports = this.sharedInstantiatedExports.SetItem(freshSharingBoundary, new Dictionary<int, object>());
                    this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.SetItem(freshSharingBoundary, new HashSet<IDisposable>());
                }
            }

            var nonDisposableWrapper = (this as ExportProviderAsExport) ?? new ExportProviderAsExport(this);
            this.NonDisposableWrapper = LazyPart.Wrap(nonDisposableWrapper);
            this.NonDisposableWrapperExportAsListOfOne = ImmutableList.Create(
                new Export(ExportProviderExportDefinition, this.NonDisposableWrapper));
            this.metadataViewProviders = new Lazy<ImmutableList<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>>(
                () => ImmutableList.CreateRange(this.GetExports<IMetadataViewProvider, IReadOnlyDictionary<string, object>>())
                    .Sort((first, second) => -GetOrderMetadata(first.Metadata).CompareTo(GetOrderMetadata(second.Metadata))));
            this.assemblyLoadProvider = new Lazy<IAssemblyLoader>(
                () => ImmutableList.CreateRange(this.GetExports<IAssemblyLoader, IReadOnlyDictionary<string, object>>())
                    .Sort((first, second) => -GetOrderMetadata(first.Metadata).CompareTo(GetOrderMetadata(second.Metadata))).Select(v => v.Value).FirstOrDefault() ?? BuiltInAssemblyLoader);
        }

        bool IDisposableObservable.IsDisposed
        {
            get { return this.isDisposed; }
        }

        protected ILazy<DelegatingExportProvider> NonDisposableWrapper { get; private set; }

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

            IEnumerable<Export> exports = importDefinition.ContractName == ExportProviderExportDefinition.ContractName
                ? this.NonDisposableWrapperExportAsListOfOne
                : this.GetExportsCore(importDefinition);

            string genericTypeDefinitionContractName;
            Type[] genericTypeArguments;
            if (ComposableCatalog.TryGetOpenGenericExport(importDefinition, out genericTypeDefinitionContractName, out genericTypeArguments))
            {
                var genericTypeImportDefinition = new ImportDefinition(genericTypeDefinitionContractName, importDefinition.Cardinality, importDefinition.Metadata, importDefinition.ExportConstraints);
                var openGenericExports = this.GetExportsCore(genericTypeImportDefinition);
                var closedGenericExports = openGenericExports.Select(export => export.CloseGenericExport(genericTypeArguments));
                exports = exports.Concat(closedGenericExports);
            }

            var filteredExports = from export in exports
                                  where importDefinition.ExportConstraints.All(c => c.IsSatisfiedBy(export.Definition))
                                  select export;

            var exportsSnapshot = filteredExports.ToArray(); // avoid redoing the above work during multiple enumerations of our result.
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
        protected abstract IEnumerable<Export> GetExportsCore(ImportDefinition importDefinition);

        protected Export CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> metadata, int partOpenGenericTypeId, Type valueFactoryMethodDeclaringType, string valueFactoryMethodName, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(metadata, "metadata");

            var typeArgs = (Type[])importDefinition.Metadata[CompositionConstants.GenericParametersMetadataName];
            var valueFactoryOpenGenericMethodInfo = this.GetMethodWithArity(valueFactoryMethodDeclaringType, valueFactoryMethodName, typeArgs.Length);
            var valueFactoryMethodInfo = valueFactoryOpenGenericMethodInfo.MakeGenericMethod(typeArgs);
            var valueFactory = (Func<ExportProvider, Dictionary<int, object>, object>)valueFactoryMethodInfo.CreateDelegate(typeof(Func<ExportProvider, Dictionary<int, object>, object>), null);

            Type partOpenGenericType = this.GetType(partOpenGenericTypeId);
            Type partType = partOpenGenericType.MakeGenericType(typeArgs);
            int partTypeId = this.GetTypeId(partType);

            return this.CreateExport(importDefinition, metadata, partTypeId, valueFactory, partSharingBoundary, nonSharedInstanceRequired, exportingMember);
        }

        protected Export CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> metadata, int partTypeId, Func<ExportProvider, Dictionary<int, object>, object> valueFactory, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(metadata, "metadata");
            Requires.NotNull(valueFactory, "valueFactory");

            var provisionalSharedObjects = new Dictionary<int, object>();
            ILazy<object> lazy = this.GetOrCreateShareableValue(partTypeId, valueFactory, provisionalSharedObjects, partSharingBoundary, nonSharedInstanceRequired);
            Func<object> memberValueFactory;
            if (exportingMember == null)
            {
                memberValueFactory = lazy.ValueFactory;
            }
            else
            {
                memberValueFactory = () => GetValueFromMember(lazy.Value, exportingMember);
            }

            return new Export(importDefinition.ContractName, metadata, memberValueFactory);
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

        protected ILazy<object> GetOrCreateShareableValue(int partTypeId, Func<ExportProvider, Dictionary<int, object>, object> valueFactory, Dictionary<int, object> provisionalSharedObjects, string partSharingBoundary, bool nonSharedInstanceRequired)
        {
            ILazy<System.Object> lazyResult;
            if (!nonSharedInstanceRequired)
            {
                if (this.TryGetProvisionalSharedExport(provisionalSharedObjects, partTypeId, out lazyResult) ||
                    this.TryGetSharedInstanceFactory(partSharingBoundary, partTypeId, out lazyResult))
                {
                    return lazyResult;
                }
            }

            lazyResult = new LazyPart<object>(() => valueFactory(this, provisionalSharedObjects));

            if (!nonSharedInstanceRequired)
            {
                lazyResult = this.GetOrAddSharedInstanceFactory(partSharingBoundary, partTypeId, lazyResult);
            }

            return lazyResult;
        }

        private bool TryGetSharedInstanceFactory<T>(string partSharingBoundary, int partTypeId, out ILazy<T> value)
        {
            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                object valueObject;
                bool result = sharingBoundary.TryGetValue(partTypeId, out valueObject);
                value = (ILazy<T>)valueObject;
                return result;
            }
        }

        private ILazy<object> GetOrAddSharedInstanceFactory(string partSharingBoundary, int partTypeId, ILazy<object> value)
        {
            Requires.NotNull(value, "value");

            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                object priorValue;
                if (sharingBoundary.TryGetValue(partTypeId, out priorValue))
                {
                    return (ILazy<object>)priorValue;
                }

                sharingBoundary.Add(partTypeId, value);
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

        /// <summary>
        /// Gets the manifest module for an assembly.
        /// </summary>
        /// <param name="assemblyId">The index into the cached manifest array.</param>
        /// <returns>The manifest module.</returns>
        protected Module GetAssemblyManifest(int assemblyId)
        {
            Module result = cachedManifests[assemblyId];
            if (result == null)
            {
                // We have to be very careful about getting the assembly loader because it may itself be
                // a MEF component that is in an assembly that must be loaded.
                // So we'll go ahead and try to use the right loader, but if we get re-entered in the meantime,
                // on the same thread, we'll fallback to using our built-in one.
                // The requirement then is that any assembly loader provider must be in an assembly that can be
                // loaded using our built-in one.
                IAssemblyLoader loader;
                if (!this.assemblyLoadProvider.IsValueCreated)
                {
                    if (this.initializingAssemblyLoader.Value)
                    {
                        loader = BuiltInAssemblyLoader;
                    }
                    else
                    {
                        this.initializingAssemblyLoader.Value = true;
                        try
                        {
                            loader = this.assemblyLoadProvider.Value;
                        }
                        finally
                        {
                            this.initializingAssemblyLoader.Value = false;
                        }
                    }
                }
                else
                {
                    loader = this.assemblyLoadProvider.Value;
                }

                Assembly assembly = loader.LoadAssembly(
                    this.assemblyNames[assemblyId],
                    this.assemblyCodeBasePaths[assemblyId]);

                // We don't need to worry about thread-safety here because if two threads assign the
                // reference to the loaded assembly to the array slot, that's just fine.
                result = assembly.ManifestModule;
                cachedManifests[assemblyId] = result;
            }

            return result;
        }

        /// <summary>
        /// Gets a type for reflection.
        /// </summary>
        /// <param name="typeId">The index into the cached type array.</param>
        /// <returns>The type.</returns>
        protected Type GetType(int typeId)
        {
            Type result = typeId < this.cachedTypes.Length
                ? this.cachedTypes[typeId]
                : Reflection.Resolver.Resolve(this.runtimeCreatedTypes[typeId - this.cachedTypes.Length]);
            if (result == null)
            {
                // We don't need to worry about thread-safety here because if two threads assign the
                // reference to the type to the array slot, that's just fine.
                result = this.GetTypeCore(typeId);
                this.cachedTypes[typeId] = result;
            }

            return result;
        }

        protected int GetTypeId(object value)
        {
            Requires.NotNull(value, "value");
            return this.GetTypeId(value.GetType());
        }

        protected int GetTypeId(Type type)
        {
            Requires.NotNull(type, "type");
            return this.GetTypeId(Reflection.TypeRef.Get(type));
        }

        protected int GetTypeId(Reflection.TypeRef type)
        {
            Requires.NotNull(type, "type");

            int index = this.GetTypeIdCore(type);
            if (index < 0)
            {
                // This type isn't one that the precompiled code knew about.
                // This can happen when an open generic export is queried for
                // using ExportProvider.GetExportedValue<SomePart<T>>().
                // Is this an extra type we have already seen and have an index for?
                int privateIndex = this.runtimeCreatedTypes.IndexOf(type);
                if (privateIndex < 0)
                {
                    lock (this.syncObject)
                    {
                        privateIndex = this.runtimeCreatedTypes.IndexOf(type);
                        if (privateIndex < 0)
                        {
                            // We need to add the type to some array and assign a dedicated index for it
                            // that does not overlap with anything the precompiled assembly has.
                            this.runtimeCreatedTypes.Add(type);
                            privateIndex = this.runtimeCreatedTypes.Count - 1;
                        }
                    }
                }

                return this.cachedTypes.Length + privateIndex;
            }

            return index;
        }

        /// <summary>
        /// When overridden in the derived code-gen'd class, this method gets the type
        /// for an integer that the code-gen knows about.
        /// </summary>
        protected virtual Type GetTypeCore(int typeId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// When overridden in the derived code-gen'd class, this method gets the index
        /// into the array that is designated for the specified type.
        /// </summary>
        /// <returns>A non-negative integer if a type match is found; otherwise a negative integer.</returns>
        protected virtual int GetTypeIdCore(Reflection.TypeRef type)
        {
            throw new NotImplementedException();
        }

        protected internal interface IMetadataDictionary : IDictionary<string, object>, IReadOnlyDictionary<string, object> { }

        protected IMetadataDictionary GetTypeRefResolvingMetadata(ImmutableDictionary<string, object> metadata)
        {
            Requires.NotNull(metadata, "metadata");
            return new ExportProviderLazyMetadataWrapper(this, metadata);
        }

        protected static IReadOnlyDictionary<string, object> AddMissingValueDefaults(Type metadataView, IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(metadataView, "metadataView");
            Requires.NotNull(metadata, "metadata");

            if (metadataView.GetTypeInfo().IsInterface && !metadataView.Equals(typeof(IDictionary<string, object>)))
            {
                var metadataBuilder = LazyMetadataWrapper.TryUnwrap(metadata).ToImmutableDictionary().ToBuilder();
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

                return LazyMetadataWrapper.Rewrap(metadata, metadataBuilder.ToImmutable());
            }

            // No changes since the metadata view type doesn't provide any.
            return metadata;
        }

        private static int GetOrderMetadata(IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(metadata, "metadata");

            object value = metadata.GetValueOrDefault("OrderPrecedence");
            return value is int ? (int)value : 0;
        }

        private bool TryGetProvisionalSharedExport(IReadOnlyDictionary<int, object> provisionalSharedObjects, int partTypeId, out ILazy<object> value)
        {
            object valueObject;
            if (provisionalSharedObjects.TryGetValue(partTypeId, out valueObject))
            {
                value = LazyPart.Wrap(valueObject, this.GetType(partTypeId));
                return true;
            }

            value = null;
            return false;
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
                constraints = constraints.Add(ImportMetadataViewConstraint.GetConstraint(typeof(TMetadataView)));
            }

            var importMetadata = PartDiscovery.GetImportMetadataForGenericTypeImport(typeof(T));
            var importDefinition = new ImportDefinition(contractName, cardinality, importMetadata, constraints);
            IEnumerable<Export> results = this.GetExports(importDefinition);
            return results.Select(result => new LazyPart<T, TMetadataView>(
                () => result.Value,
                (TMetadataView)metadataViewProvider.CreateProxy(
                    metadataViewProvider.IsDefaultMetadataRequired ? AddMissingValueDefaults(typeof(TMetadataView), result.Metadata) : result.Metadata,
                    typeof(TMetadataView))))
                .ToImmutableHashSet();
        }

        /// <summary>
        /// Gets a provider that can create a metadata view of a specified type over a dictionary of metadata.
        /// </summary>
        /// <param name="metadataView">The type of metadata view required.</param>
        /// <returns>A metadata view provider.</returns>
        /// <exception cref="NotSupportedException">Thrown if no metadata view provider available is compatible with the type.</exception>
        internal IMetadataViewProvider GetMetadataViewProvider(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            IMetadataViewProvider metadataViewProvider = BuiltInMetadataViewProviders
                .FirstOrDefault(vp => vp.IsMetadataViewSupported(metadataView));
            if (metadataViewProvider != null)
            {
                return metadataViewProvider;
            }

            metadataViewProvider = this.metadataViewProviders.Value
                    .Select(vp => vp.Value)
                    .FirstOrDefault(vp => vp.IsMetadataViewSupported(metadataView));
            if (metadataViewProvider == null)
            {
                throw new NotSupportedException("Type of metadata view is unsupported.");
            }

            return metadataViewProvider;
        }

        private Dictionary<int, object> AcquireSharingBoundaryInstances(string sharingBoundaryName)
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

        protected internal struct TypeRef
        {
            public TypeRef(int typeId)
                : this()
            {
                this.TypeId = typeId;
            }

            public int TypeId { get; private set; }

            public Type GetType(ExportProvider resolvingExportProvider)
            {
                Requires.NotNull(resolvingExportProvider, "resolvingExportProvider");

                return resolvingExportProvider.GetType(this.TypeId);
            }
        }

        private class ExportProviderLazyMetadataWrapper : LazyMetadataWrapper
        {
            private readonly ExportProvider resolvingExportProvider;

            internal ExportProviderLazyMetadataWrapper(ExportProvider resolvingExportProvider, ImmutableDictionary<string, object> metadata)
                : base(metadata)
            {
                Requires.NotNull(resolvingExportProvider, "resolvingExportProvider");

                this.resolvingExportProvider = resolvingExportProvider;
            }

            protected override object SubstituteValueIfRequired(string key, object value)
            {
                value = base.SubstituteValueIfRequired(key, value);

                if (value is ExportProvider.TypeRef)
                {
                    value = ((ExportProvider.TypeRef)value).GetType(this.resolvingExportProvider);
                }
                else if (value is ExportProvider.TypeRef[])
                {
                    value = ((ExportProvider.TypeRef[])value).Select(r => r.GetType(this.resolvingExportProvider)).ToArray();

                    // Update our metadata dictionary with the substitution to avoid
                    // the translation costs next time.
                    this.underlyingMetadata = this.underlyingMetadata.SetItem(key, value);
                }

                return value;
            }

            protected override LazyMetadataWrapper Clone(LazyMetadataWrapper oldVersion, IReadOnlyDictionary<string, object> newMetadata)
            {
                return new ExportProviderLazyMetadataWrapper(((ExportProviderLazyMetadataWrapper)oldVersion).resolvingExportProvider, newMetadata.ToImmutableDictionary());
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

        /// <summary>
        /// Supports metadata views that are any type that <see cref="ImmutableDictionary{TKey, TValue}"/>
        /// could be assigned to, including <see cref="IDictionary{TKey, TValue}"/> and <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
        /// </summary>
        private class PassthroughMetadataViewProvider : IMetadataViewProvider
        {
            private PassthroughMetadataViewProvider() { }

            internal static readonly IMetadataViewProvider Default = new PassthroughMetadataViewProvider();

            public bool IsDefaultMetadataRequired
            {
                get { return false; }
            }

            public bool IsMetadataViewSupported(Type metadataType)
            {
                Requires.NotNull(metadataType, "metadataType");

                return metadataType.GetTypeInfo().IsAssignableFrom(typeof(IReadOnlyDictionary<string, object>).GetTypeInfo())
                    || metadataType.GetTypeInfo().IsAssignableFrom(typeof(IDictionary<string, object>).GetTypeInfo());
            }

            public object CreateProxy(IReadOnlyDictionary<string, object> metadata, Type metadataViewType)
            {
                Requires.NotNull(metadata, "metadata");

                // This cast should work because our IsMetadataViewSupported method filters to those that do.
                return metadata;
            }
        }

        /// <summary>
        /// Supports metadata views that are concrete classes with a public constructor
        /// that accepts the metadata dictionary as its only parameter.
        /// </summary>
        private class MetadataViewClassProvider : IMetadataViewProvider
        {
            private MetadataViewClassProvider() { }

            internal static readonly IMetadataViewProvider Default = new MetadataViewClassProvider();

            public bool IsDefaultMetadataRequired
            {
                get { return false; }
            }

            public bool IsMetadataViewSupported(Type metadataType)
            {
                Requires.NotNull(metadataType, "metadataType");
                var typeInfo = metadataType.GetTypeInfo();

                return typeInfo.IsClass && !typeInfo.IsAbstract && FindConstructor(typeInfo) != null;
            }

            public object CreateProxy(IReadOnlyDictionary<string, object> metadata, Type metadataViewType)
            {
                return FindConstructor(metadataViewType.GetTypeInfo())
                    .Invoke(new object[] { ImmutableDictionary.CreateRange(metadata) });
            }

            private static ConstructorInfo FindConstructor(TypeInfo metadataType)
            {
                Requires.NotNull(metadataType, "metadataType");

                var publicCtorsWithOneParameter = from ctor in metadataType.DeclaredConstructors
                                                  where ctor.IsPublic
                                                  let parameters = ctor.GetParameters()
                                                  where parameters.Length == 1
                                                  let paramInfo = parameters[0].ParameterType.GetTypeInfo()
                                                  where paramInfo.IsAssignableFrom(typeof(ImmutableDictionary<string, object>).GetTypeInfo())
                                                  select ctor;
                return publicCtorsWithOneParameter.FirstOrDefault();
            }
        }

        private class AssemblyLoaderByFullName : IAssemblyLoader
        {
            public Assembly LoadAssembly(string assemblyFullName, string codeBasePath)
            {
                // We can't use codeBasePath here because this is a PCL, and the
                // facade assembly we reference doesn't expose AssemblyName.CodeBasePath.
                // That's why the MS.VS.Composition.Configuration.dll has another IAssemblyLoader
                // that we prefer over this one. It does the codebasepath thing.
                return Assembly.Load(new AssemblyName(assemblyFullName));
            }
        }
    }
}
