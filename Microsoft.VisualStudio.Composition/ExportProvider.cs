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
    using System.Threading.Tasks;
    using Validation;
    using DefaultMetadataType = System.Collections.Generic.IDictionary<string, object>;

    public abstract class ExportProvider : IDisposableObservable
    {
        internal static readonly ExportDefinition ExportProviderExportDefinition = new ExportDefinition(
            ContractNameServices.GetTypeIdentity(typeof(ExportProvider)),
            PartCreationPolicyConstraint.GetExportMetadata(CreationPolicy.Shared).AddRange(ExportTypeIdentityConstraint.GetExportMetadata(typeof(ExportProvider))));

        internal static readonly ComposablePartDefinition ExportProviderPartDefinition = new ComposablePartDefinition(
            typeof(ExportProviderAsExport),
            new[] { ExportProviderExportDefinition },
            ImmutableDictionary<MemberInfo, IReadOnlyList<ExportDefinition>>.Empty,
            ImmutableList<ImportDefinitionBinding>.Empty,
            null,
            null,
            CreationPolicy.Shared);

        protected static readonly LazyPart<object> NotInstantiablePartLazy = new LazyPart<object>(() => CannotInstantiatePartWithNoImportingConstructor());

        protected static readonly object[] EmptyObjectArray = new object[0];

        /// <summary>
        /// A metadata template used by the generated code.
        /// </summary>
        protected static readonly ImmutableDictionary<string, object> EmptyMetadata = ImmutableDictionary.Create<string, object>();

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

        /// <summary>
        /// The metadata view providers available to this ExportProvider.
        /// </summary>
        /// <remarks>
        /// This field is lazy to avoid a chicken-and-egg problem with initializing it in our constructor.
        /// </remarks>
        private readonly Lazy<ImmutableList<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>> metadataViewProviders;

        /// <summary>
        /// An array of types 
        /// </summary>
        private List<Type> runtimeCreatedTypes = new List<Type>();

        private readonly object syncObject = new object();

        /// <summary>
        /// A map of shared boundary names to their shared instances.
        /// The value is a dictionary of types to their Lazy{T} factories.
        /// </summary>
        private readonly ImmutableDictionary<string, Dictionary<int, object>> sharedInstantiatedExports = ImmutableDictionary.Create<string, Dictionary<int, object>>();

        /// <summary>
        /// The disposable objects whose lifetimes are controlled by this instance.
        /// </summary>
        private readonly HashSet<IDisposable> disposableInstantiatedParts = new HashSet<IDisposable>();

        private bool isDisposed;

        protected ExportProvider(ExportProvider parent, string[] freshSharingBoundaries)
        {
            if (parent == null)
            {
                this.sharedInstantiatedExports = this.sharedInstantiatedExports.Add(string.Empty, new Dictionary<int, object>());
            }
            else
            {
                this.sharedInstantiatedExports = parent.sharedInstantiatedExports;
            }

            if (freshSharingBoundaries != null)
            {
                foreach (string freshSharingBoundary in freshSharingBoundaries)
                {
                    this.sharedInstantiatedExports = this.sharedInstantiatedExports.SetItem(freshSharingBoundary, new Dictionary<int, object>());
                }
            }

            var nonDisposableWrapper = (this as ExportProviderAsExport) ?? new ExportProviderAsExport(this);
            this.NonDisposableWrapper = LazyPart.Wrap(nonDisposableWrapper);
            this.NonDisposableWrapperExportAsListOfOne = ImmutableList.Create(
                new Export(ExportProviderExportDefinition, this.NonDisposableWrapper));
            this.metadataViewProviders = new Lazy<ImmutableList<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>>(
                () => ImmutableList.CreateRange(this.GetExports<IMetadataViewProvider, IReadOnlyDictionary<string, object>>())
                    .Sort((first, second) => -GetOrderMetadata(first.Metadata).CompareTo(GetOrderMetadata(second.Metadata))));
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
                lock (this.syncObject)
                {
                    disposableSnapshot = new List<IDisposable>(this.disposableInstantiatedParts);
                    this.disposableInstantiatedParts.Clear();
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
        /// When implemented by a derived class, returns an <see cref="IEnumerable&lt;ILazy&lt;T&gt;&gt;"/> of values that
        /// satisfy the contract name of the specified <see cref="ImportDefinition"/>.
        /// </summary>
        /// <remarks>
        /// The derived type is *not* expected to filter the exports based on the import definition constraints.
        /// </remarks>
        protected abstract IEnumerable<Export> GetExportsCore(ImportDefinition importDefinition);

        protected Export CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> metadata, int partOpenGenericTypeId, string valueFactoryMethodName, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(metadata, "metadata");

            var typeArgs = (Type[])importDefinition.Metadata[CompositionConstants.GenericParametersMetadataName];
            var valueFactoryOpenGenericMethodInfo = this.GetMethodWithArity(valueFactoryMethodName, typeArgs.Length);
            var valueFactoryMethodInfo = valueFactoryOpenGenericMethodInfo.MakeGenericMethod(typeArgs);
            var valueFactory = (Func<Dictionary<int, object>, object>)valueFactoryMethodInfo.CreateDelegate(typeof(Func<Dictionary<int, object>, object>), this);

            Type partOpenGenericType = this.GetType(partOpenGenericTypeId);
            Type partType = partOpenGenericType.MakeGenericType(typeArgs);
            int partTypeId = this.GetTypeId(partType);

            return this.CreateExport(importDefinition, metadata, partTypeId, valueFactory, partSharingBoundary, nonSharedInstanceRequired, exportingMember);
        }

        protected Export CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> metadata, int partTypeId, Func<Dictionary<int, object>, object> valueFactory, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
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

        private object GetValueFromMember(object instance, MemberInfo member)
        {
            Requires.NotNull(instance, "instance");
            Requires.NotNull(member, "member");

            var field = member as FieldInfo;
            if (field != null)
            {
                return field.GetValue(instance);
            }

            var property = member as PropertyInfo;
            if (property != null)
            {
                return property.GetValue(instance);
            }

            var method = member as MethodInfo;
            if (method != null)
            {
                // If the method came from a property, return the result of the property getter rather than return the delegate.
                if (method.IsSpecialName && method.GetParameters().Length == 0 && method.Name.StartsWith("get_"))
                {
                    return method.Invoke(instance, EmptyObjectArray);
                }

                return method.CreateDelegate(ExportDefinitionBinding.GetContractTypeForDelegate(method), method.IsStatic ? null : instance);
            }

            throw new NotSupportedException();
        }

        protected ILazy<object> GetOrCreateShareableValue(int partTypeId, Func<Dictionary<int, object>, object> valueFactory, Dictionary<int, object> provisionalSharedObjects, string partSharingBoundary, bool nonSharedInstanceRequired)
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

            lazyResult = new LazyPart<object>(() => valueFactory(provisionalSharedObjects));

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

        protected void TrackDisposableValue(IDisposable value)
        {
            Requires.NotNull(value, "value");

            lock (this.syncObject)
            {
                this.disposableInstantiatedParts.Add(value);
            }
        }

        protected MethodInfo GetMethodWithArity(string methodName, int arity)
        {
            return this.GetType().GetTypeInfo().GetDeclaredMethods(methodName)
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
                // We don't need to worry about thread-safety here because if two threads assign the
                // reference to the loaded assembly to the array slot, that's just fine.
                result = Assembly.Load(new AssemblyName(this.GetAssemblyName(assemblyId))).ManifestModule;
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
                : this.runtimeCreatedTypes[typeId - this.cachedTypes.Length];
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
        /// When overridden in the derived code-gen'd class, this method gets the full name
        /// of an assembly for an integer that the code-gen knows about.
        /// </summary>
        protected virtual string GetAssemblyName(int assemblyId)
        {
            throw new NotImplementedException();
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
        protected virtual int GetTypeIdCore(Type type)
        {
            throw new NotImplementedException();
        }

        private static IReadOnlyDictionary<string, object> AddMissingValueDefaults(Type metadataView, IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(metadataView, "metadataView");
            Requires.NotNull(metadata, "metadata");

            if (metadataView.GetTypeInfo().IsInterface && !metadataView.Equals(typeof(IDictionary<string, object>)))
            {
                var metadataBuilder = metadata.ToImmutableDictionary().ToBuilder();
                foreach (var property in metadataView.EnumProperties().WherePublicInstance())
                {
                    if (!metadataBuilder.ContainsKey(property.Name))
                    {
                        var defaultValueAttribute = property.GetCustomAttributes<DefaultValueAttribute>().FirstOrDefault();
                        if (defaultValueAttribute != null)
                        {
                            metadataBuilder.Add(property.Name, defaultValueAttribute.Value);
                        }
                    }
                }

                return metadataBuilder.ToImmutable();
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
                constraints = constraints.Add(new ImportMetadataViewConstraint(typeof(TMetadataView)));
            }

            var importMetadata = PartDiscovery.GetImportMetadataForGenericTypeImport(typeof(T));
            var importDefinition = new ImportDefinition(contractName, cardinality, importMetadata, constraints);
            IEnumerable<Export> results = this.GetExports(importDefinition);
            return results.Select(result => new LazyPart<T, TMetadataView>(
                () => result.Value,
                metadataViewProvider.CreateProxy<TMetadataView>(metadataViewProvider.IsDefaultMetadataRequired ? AddMissingValueDefaults(typeof(TMetadataView), result.Metadata) : result.Metadata)))
                .ToImmutableHashSet();
        }

        /// <summary>
        /// Gets a provider that can create a metadata view of a specified type over a dictionary of metadata.
        /// </summary>
        /// <param name="metadataView">The type of metadata view required.</param>
        /// <returns>A metadata view provider.</returns>
        /// <exception cref="NotSupportedException">Thrown if no metadata view provider available is compatible with the type.</exception>
        private IMetadataViewProvider GetMetadataViewProvider(Type metadataView)
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

                return metadataType.GetTypeInfo().IsAssignableFrom(typeof(ImmutableDictionary<string, object>).GetTypeInfo());
            }

            public TMetadata CreateProxy<TMetadata>(IReadOnlyDictionary<string, object> metadata)
            {
                Requires.NotNull(metadata, "metadata");

                // This cast should work because our IsMetadataViewSupported method filters to those that do.
                return (TMetadata)(object)ImmutableDictionary.CreateRange(metadata);
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

            public TMetadata CreateProxy<TMetadata>(IReadOnlyDictionary<string, object> metadata)
            {
                return (TMetadata)FindConstructor(typeof(TMetadata).GetTypeInfo())
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
    }
}
