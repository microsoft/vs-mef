// Copyright (c) Microsoft. All rights reserved.

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
    using DefaultMetadataType = System.Collections.Generic.IDictionary<string, object>;

    public abstract class ExportProvider : IDisposableObservable
    {
        internal static readonly ExportDefinition ExportProviderExportDefinition = new ExportDefinition(
            ContractNameServices.GetTypeIdentity(typeof(ExportProvider)),
            PartCreationPolicyConstraint.GetExportMetadata(CreationPolicy.Shared).AddRange(ExportTypeIdentityConstraint.GetExportMetadata(typeof(ExportProvider))));

        internal static readonly ComposablePartDefinition ExportProviderPartDefinition = new ComposablePartDefinition(
            TypeRef.Get(typeof(ExportProviderAsExport), Resolver.DefaultInstance),
            ImmutableDictionary<string, object>.Empty.Add(CompositionConstants.DgmlCategoryPartMetadataName, new[] { "VsMEFBuiltIn" }),
            new[] { ExportProviderExportDefinition },
            ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Empty,
            ImmutableList<ImportDefinitionBinding>.Empty,
            string.Empty,
            default(MethodRef),
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

        private static readonly Dictionary<Type, IReadOnlyDictionary<string, object>> GetMetadataViewDefaultsCache = new Dictionary<Type, IReadOnlyDictionary<string, object>>();

        private static readonly ImmutableDictionary<string, Dictionary<TypeRef, PartLifecycleTracker>> SharedInstantiatedPartsTemplate = ImmutableDictionary.Create<string, Dictionary<TypeRef, PartLifecycleTracker>>().Add(string.Empty, new Dictionary<TypeRef, PartLifecycleTracker>());

        private static readonly ImmutableDictionary<string, HashSet<IDisposable>> DisposableInstantiatedSharedPartsTemplate = ImmutableDictionary.Create<string, HashSet<IDisposable>>().Add(string.Empty, new HashSet<IDisposable>());

        /// <summary>
        /// The metadata view providers available to this ExportProvider.
        /// </summary>
        /// <remarks>
        /// This field is lazy to avoid a chicken-and-egg problem with initializing it in our constructor.
        /// </remarks>
        private readonly Lazy<ImmutableArray<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>> metadataViewProviders;

        /// <summary>
        /// A map of shared boundary names to their shared instances.
        /// The value is a dictionary of types to their lazily-constructed instances and state.
        /// </summary>
        private readonly ImmutableDictionary<string, Dictionary<TypeRef, PartLifecycleTracker>> sharedInstantiatedParts;

        /// <summary>
        /// A map of sharing boundary names to the ExportProvider that owns them.
        /// </summary>
        private readonly ImmutableDictionary<string, ExportProvider> sharingBoundaryExportProviderOwners;

        /// <summary>
        /// The disposable objects whose lifetimes are shared and tied to a specific sharing boundary.
        /// </summary>
        private readonly ImmutableDictionary<string, HashSet<IDisposable>> disposableInstantiatedSharedParts;

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

        /// <summary>
        /// A cache for the <see cref="GetMetadataViewProvider"/> method which has shown up on perf traces.
        /// </summary>
        /// <remarks>
        /// All access to this dictionary is guarded by a lock on this field.
        /// </remarks>
        private Dictionary<Type, IMetadataViewProvider> typeAndSelectedMetadataViewProviderCache = new Dictionary<Type, IMetadataViewProvider>();

        private bool isDisposed;

        private ExportProvider(
            Resolver resolver,
            ImmutableDictionary<string, Dictionary<TypeRef, PartLifecycleTracker>> sharedInstantiatedParts,
            ImmutableDictionary<string, HashSet<IDisposable>> disposableInstantiatedSharedParts,
            ImmutableHashSet<string> freshSharingBoundaries,
            ImmutableDictionary<string, ExportProvider> sharingBoundaryExportProviderOwners,
            Lazy<ImmutableArray<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>> inheritedMetadataViewProviders)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(sharedInstantiatedParts, nameof(sharedInstantiatedParts));
            Requires.NotNull(disposableInstantiatedSharedParts, nameof(disposableInstantiatedSharedParts));
            Requires.NotNull(freshSharingBoundaries, nameof(freshSharingBoundaries));
            Requires.NotNull(sharingBoundaryExportProviderOwners, nameof(sharingBoundaryExportProviderOwners));

            this.Resolver = resolver;
            this.sharedInstantiatedParts = sharedInstantiatedParts;
            this.disposableInstantiatedSharedParts = disposableInstantiatedSharedParts;
            this.freshSharingBoundaries = freshSharingBoundaries;
            this.sharingBoundaryExportProviderOwners = sharingBoundaryExportProviderOwners;

            foreach (string freshSharingBoundary in freshSharingBoundaries)
            {
                this.sharedInstantiatedParts = this.sharedInstantiatedParts.SetItem(freshSharingBoundary, new Dictionary<TypeRef, PartLifecycleTracker>());
                this.disposableInstantiatedSharedParts = this.disposableInstantiatedSharedParts.SetItem(freshSharingBoundary, new HashSet<IDisposable>());
            }

            this.sharingBoundaryExportProviderOwners = this.sharingBoundaryExportProviderOwners.SetItems(
                this.freshSharingBoundaries.Select(boundary => new KeyValuePair<string, ExportProvider>(boundary, this)));

            var nonDisposableWrapper = (this as ExportProviderAsExport) ?? new ExportProviderAsExport(this);
            this.NonDisposableWrapper = LazyServices.FromValue<object>(nonDisposableWrapper);
            this.NonDisposableWrapperExportAsListOfOne = ImmutableList.Create(
                new Export(ExportProviderExportDefinition, this.NonDisposableWrapper));
            this.metadataViewProviders = inheritedMetadataViewProviders
                ?? new Lazy<ImmutableArray<Lazy<IMetadataViewProvider, IReadOnlyDictionary<string, object>>>>(
                    this.GetMetadataViewProviderExtensions);
        }

        protected ExportProvider(Resolver resolver)
            : this(
                resolver,
                SharedInstantiatedPartsTemplate,
                DisposableInstantiatedSharedPartsTemplate,
                ImmutableHashSet.Create<string>().Add(string.Empty),
                ImmutableDictionary.Create<string, ExportProvider>(),
                null)
        {
        }

        protected ExportProvider(ExportProvider parent, ImmutableHashSet<string> freshSharingBoundaries)
            : this(
                  Requires.NotNull(parent, nameof(parent)).Resolver,
                  parent.sharedInstantiatedParts,
                  parent.disposableInstantiatedSharedParts,
                  freshSharingBoundaries,
                  parent.sharingBoundaryExportProviderOwners,
                  parent.metadataViewProviders)
        {
            this.Resolver = parent.Resolver;
        }

        /// <summary>
        /// The several stages of initialization that each MEF part goes through.
        /// </summary>
        protected internal enum PartLifecycleState
        {
            /// <summary>
            /// The MEF part has not yet been instantiated.
            /// </summary>
            NotCreated,

            /// <summary>
            /// The MEF part's importing constructor is being invoked.
            /// </summary>
            Creating,

            /// <summary>
            /// The MEF part has been instantiated.
            /// </summary>
            Created,

            /// <summary>
            /// The MEF part's importing members have been satisfied.
            /// </summary>
            ImmediateImportsSatisfied,

            /// <summary>
            /// All MEF parts reachable from this one (through non-lazy import paths) have been satisfied.
            /// </summary>
            ImmediateImportsSatisfiedTransitively,

            /// <summary>
            /// The MEF part's OnImportsSatisfied method is being invoked.
            /// </summary>
            OnImportsSatisfiedInProgress,

            /// <summary>
            /// The MEF part's OnImportsSatisfied method has been invoked (or would have if one was defined).
            /// </summary>
            OnImportsSatisfiedInvoked,

            /// <summary>
            /// The OnImportsSatisfied methods on this and all MEF parts reachable from this one (through non-lazy import paths) have been invoked.
            /// </summary>
            OnImportsSatisfiedInvokedTransitively,

            /// <summary>
            /// This part is ready for exposure to the user.
            /// </summary>
            Final,
        }

        protected internal interface IMetadataDictionary : IDictionary<string, object>, IReadOnlyDictionary<string, object>
        {
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

        protected internal Resolver Resolver { get; }

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
            Requires.NotNull(importDefinition, nameof(importDefinition));

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
                throw new CompositionFailedException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UnexpectedNumberOfExportsFound,
                        1,
                        importDefinition.ContractName,
                        exportsSnapshot.Length));
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

                // Take care to give all disposal parts a chance to dispose
                // even if some parts throw exceptions.
                List<Exception> exceptions = null;
                foreach (var item in disposableSnapshot)
                {
                    try
                    {
                        item.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (exceptions == null)
                        {
                            exceptions = new List<Exception>();
                        }

                        exceptions.Add(ex);
                    }
                }

                if (exceptions != null)
                {
                    throw new AggregateException(Strings.ContainerDisposalEncounteredExceptions, exceptions);
                }
            }
        }

        protected static object CannotInstantiatePartWithNoImportingConstructor()
        {
            throw new CompositionFailedException(Strings.NoImportingConstructor);
        }

        /// <summary>
        /// Gets a value indicating whether an import with the given characteristics must be initially satisfied
        /// with a fully pre-initialized export.
        /// </summary>
        /// <param name="importingPartTracker">The tracker for the part that is importing.</param>
        /// <param name="isLazy"><c>true</c> if the import is a Lazy{T} style import; <c>false</c> otherwise.</param>
        /// <param name="isImportingConstructorArgument"><c>true</c> if the import appears in an importing constructor; <c>false</c> otherwise.</param>
        /// <returns>
        /// <c>true</c> if the export must have its imports transitively satisfied and OnImportsSatisfied methods invoked
        /// prior to being exposed to the receiver; <c>false</c> if the export can be partially initialized when the receiver
        /// first observes it.
        /// </returns>
        protected static bool IsFullyInitializedExportRequiredWhenSettingImport(PartLifecycleTracker importingPartTracker, bool isLazy, bool isImportingConstructorArgument)
        {
            // Only non-lazy importing properties can receive exports that are only partially initialized.
            return isLazy || isImportingConstructorArgument;
        }

        /// <summary>
        /// When implemented by a derived class, returns an <see cref="IEnumerable{T}"/> of values that
        /// satisfy the contract name of the specified <see cref="ImportDefinition"/>.
        /// </summary>
        /// <remarks>
        /// The derived type is *not* expected to filter the exports based on the import definition constraints.
        /// </remarks>
        protected abstract IEnumerable<ExportInfo> GetExportsCore(ImportDefinition importDefinition);

        protected ExportInfo CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> exportMetadata, TypeRef originalPartTypeRef, TypeRef constructedPartTypeRef, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));
            Requires.NotNull(exportMetadata, "metadata");
            Requires.NotNull(originalPartTypeRef, nameof(originalPartTypeRef));
            Requires.NotNull(constructedPartTypeRef, nameof(constructedPartTypeRef));

            Func<object> memberValueFactory;
            if (exportingMember == null)
            {
                memberValueFactory = () =>
                {
                    PartLifecycleTracker maybeSharedValueFactory = this.GetOrCreateValue(originalPartTypeRef, constructedPartTypeRef, partSharingBoundary, importDefinition.Metadata, nonSharedInstanceRequired);
                    return maybeSharedValueFactory.GetValueReadyToExpose();
                };
            }
            else
            {
                memberValueFactory = () =>
                {
                    PartLifecycleTracker maybeSharedValueFactory = this.GetOrCreateValue(originalPartTypeRef, constructedPartTypeRef, partSharingBoundary, importDefinition.Metadata, nonSharedInstanceRequired);
                    return GetValueFromMember(maybeSharedValueFactory.GetValueReadyToRetrieveExportingMembers(), exportingMember);
                };
            }

            return new ExportInfo(importDefinition.ContractName, exportMetadata, memberValueFactory);
        }

        protected object CreateExportFactory(Type importingSiteElementType, IReadOnlyCollection<string> sharingBoundaries, Func<KeyValuePair<object, IDisposable>> valueFactory, Type exportFactoryType, IReadOnlyDictionary<string, object> exportMetadata)
        {
            Requires.NotNull(importingSiteElementType, nameof(importingSiteElementType));
            Requires.NotNull(sharingBoundaries, nameof(sharingBoundaries));
            Requires.NotNull(valueFactory, nameof(valueFactory));
            Requires.NotNull(exportFactoryType, nameof(exportFactoryType));
            Requires.NotNull(exportMetadata, nameof(exportMetadata));

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

                var ctor = exportFactoryType.GetTypeInfo().GetConstructors()[0];
                return ctor.Invoke(ctorArgs.Value);
            }
        }

        private Export CreateExportFactoryExport(ExportInfo exportInfo, Type exportFactoryType)
        {
            Requires.NotNull(exportFactoryType, nameof(exportFactoryType));

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
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(metadataType, nameof(metadataType));

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
            Requires.NotNull(exportingMember, nameof(exportingMember));

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

        protected PartLifecycleTracker GetOrCreateValue(TypeRef originalPartTypeRef, TypeRef constructedPartTypeRef, string partSharingBoundary, IReadOnlyDictionary<string, object> importMetadata, bool nonSharedInstanceRequired)
        {
            return nonSharedInstanceRequired
                ? this.CreateNewValue(originalPartTypeRef, constructedPartTypeRef, partSharingBoundary, importMetadata)
                : this.GetOrCreateShareableValue(originalPartTypeRef, constructedPartTypeRef, partSharingBoundary, importMetadata);
        }

        protected PartLifecycleTracker GetOrCreateShareableValue(TypeRef originalPartTypeRef, TypeRef constructedPartTypeRef, string partSharingBoundary, IReadOnlyDictionary<string, object> importMetadata)
        {
            Requires.NotNull(originalPartTypeRef, nameof(originalPartTypeRef));
            Requires.NotNull(constructedPartTypeRef, nameof(constructedPartTypeRef));

            PartLifecycleTracker existingLifecycle;
            if (this.TryGetSharedInstanceFactory(partSharingBoundary, constructedPartTypeRef, out existingLifecycle))
            {
                return existingLifecycle;
            }

            var partLifecycle = this.CreateNewValue(originalPartTypeRef, constructedPartTypeRef, partSharingBoundary, importMetadata);

            // Since we have not been holding a lock, we must now reconcile the creation of this
            // shared instance with a dictionary of shared instances to make sure there is only one that survives.
            partLifecycle = this.GetOrAddSharedInstanceFactory(partSharingBoundary, constructedPartTypeRef, partLifecycle);

            return partLifecycle;
        }

        protected PartLifecycleTracker CreateNewValue(TypeRef originalPartTypeRef, TypeRef constructedPartTypeRef, string partSharingBoundary, IReadOnlyDictionary<string, object> importMetadata)
        {
            // Be careful to pass the export provider that owns the sharing boundary for this part into the value factory.
            // If we accidentally capture "this", then if this is a sub-scope ExportProvider and we're constructing
            // a parent scope shared part, then we tie the lifetime of this child scope to the lifetime of the
            // parent scoped part's value factory. If it never evaluates, we never get released even after our own disposal.
            ExportProvider owningExportProvider = partSharingBoundary != null ? this.sharingBoundaryExportProviderOwners[partSharingBoundary] : this;
            var partLifecycle = owningExportProvider.CreatePartLifecycleTracker(originalPartTypeRef, importMetadata);
            return partLifecycle;
        }

        protected internal abstract PartLifecycleTracker CreatePartLifecycleTracker(TypeRef partType, IReadOnlyDictionary<string, object> importMetadata);

        private bool TryGetSharedInstanceFactory(string partSharingBoundary, TypeRef partTypeRef, out PartLifecycleTracker value)
        {
            var sharingBoundary = this.AcquireSharingBoundaryInstances(partSharingBoundary);
            lock (sharingBoundary)
            {
                bool result = sharingBoundary.TryGetValue(partTypeRef, out value);
                return result;
            }
        }

        private PartLifecycleTracker GetOrAddSharedInstanceFactory(string partSharingBoundary, TypeRef partTypeRef, PartLifecycleTracker value)
        {
            Requires.NotNull(partTypeRef, nameof(partTypeRef));
            Requires.NotNull(value, nameof(value));

            var sharingBoundary = this.AcquireSharingBoundaryInstances(partSharingBoundary);
            lock (sharingBoundary)
            {
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
            Requires.NotNull(instantiatedPart, nameof(instantiatedPart));

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
        /// Gets a dictionary of metadata that describes all the default values supplied by a metadata view.
        /// </summary>
        /// <param name="metadataView">The metadata view type.</param>
        /// <returns>A dictionary of default metadata values.</returns>
        protected static IReadOnlyDictionary<string, object> GetMetadataViewDefaults(Type metadataView)
        {
            Requires.NotNull(metadataView, nameof(metadataView));

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
            Requires.NotNull(metadata, nameof(metadata));

            object value = metadata.GetValueOrDefault("OrderPrecedence");
            return value is int ? (int)value : 0;
        }

        private static T CastValueTo<T>(object value)
        {
            if (value is ExportedDelegate && typeof(Delegate).GetTypeInfo().IsAssignableFrom(typeof(T)))
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
            Requires.NotNull(provisionalSharedObjects, nameof(provisionalSharedObjects));
            Requires.NotNull(partTypeRef, nameof(partTypeRef));

            lock (provisionalSharedObjects)
            {
                return provisionalSharedObjects.TryGetValue(partTypeRef, out value);
            }
        }

        private IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName, ImportCardinality cardinality)
        {
            Verify.NotDisposed(this);
            contractName = string.IsNullOrEmpty(contractName) ? ContractNameServices.GetTypeIdentity(typeof(T)) : contractName;
            IMetadataViewProvider metadataViewProvider = this.GetMetadataViewProvider(typeof(TMetadataView));

            var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty
                .Union(PartDiscovery.GetExportTypeIdentityConstraints(typeof(T)));

            if (typeof(TMetadataView) != typeof(DefaultMetadataType))
            {
                constraints = constraints.Add(ImportMetadataViewConstraint.GetConstraint(TypeRef.Get(typeof(TMetadataView), this.Resolver), this.Resolver));
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
            Requires.NotNull(metadataView, nameof(metadataView));

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
                    throw new NotSupportedException(Strings.TypeOfMetadataViewUnsupported);
                }

                lock (this.typeAndSelectedMetadataViewProviderCache)
                {
                    this.typeAndSelectedMetadataViewProviderCache[metadataView] = metadataViewProvider;
                }
            }

            return metadataViewProvider;
        }

        /// <summary>
        /// Gets the shared parts dictionary with a given sharing boundary name.
        /// </summary>
        /// <param name="sharingBoundaryName">The name of the sharing boundary.</param>
        /// <returns>The dictionary containing parts and instances. Never null.</returns>
        /// <exception cref="CompositionFailedException">Thrown if the dictionary for the given sharing boundary isn't found.</exception>
        private Dictionary<TypeRef, PartLifecycleTracker> AcquireSharingBoundaryInstances(string sharingBoundaryName)
        {
            Requires.NotNull(sharingBoundaryName, nameof(sharingBoundaryName));

            var sharingBoundary = this.sharedInstantiatedParts.GetValueOrDefault(sharingBoundaryName);
            if (sharingBoundary == null)
            {
                // This means someone is trying to create a part
                // that belongs to a sharing boundary that has not yet been created.
                throw new CompositionFailedException(Strings.PartBelongsToAnotherSharingBoundary);
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
                Requires.NotNull(exportDefinition, nameof(exportDefinition));
                Requires.NotNull(exportedValueGetter, nameof(exportedValueGetter));

                this.Definition = exportDefinition;
                this.ExportedValueGetter = exportedValueGetter;
            }

            public ExportDefinition Definition { get; private set; }

            public Func<object> ExportedValueGetter { get; private set; }

            internal ExportInfo CloseGenericExport(Type[] genericTypeArguments)
            {
                Requires.NotNull(genericTypeArguments, nameof(genericTypeArguments));

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

        /// <summary>
        /// A state machine that tracks an individual instance of a MEF part.
        /// Every single instantiated MEF part (including each individual NonShared instance)
        /// has an associated instance of this class to track its lifecycle from initialization to disposal.
        /// </summary>
        protected internal abstract class PartLifecycleTracker : IDisposable
        {
            /// <summary>
            /// An object that locks when the state machine is transitioning between states.
            /// It is Pulsed after each <see cref="State"/> change.
            /// </summary>
            private readonly object syncObject = new object();

            /// <summary>
            /// The sharing boundary that the MEF part this tracker is associated with belongs to.
            /// </summary>
            private readonly string sharingBoundary;

            /// <summary>
            /// A value indicating whether this instance has been disposed of.
            /// </summary>
            private bool isDisposed;

            /// <summary>
            /// Backing field for the <see cref="Value"/> property.
            /// </summary>
            private object value;

            /// <summary>
            /// A collection of all immediate imports (property and constructor) as they are satisfied
            /// if by an exporting part that has not been fully initialized already.
            /// It is nulled out upon reaching the final stage of initialization.
            /// </summary>
            /// <remarks>
            /// This collection is populated from the <see cref="PartLifecycleState.Creating"/>
            /// and <see cref="PartLifecycleState.ImmediateImportsSatisfied"/> stages, each of which
            /// occur on a single thread. It is then enumerated from <see cref="MoveToStateTransitively"/>
            /// which *may* be invoked from multiple threads.
            /// </remarks>
            private HashSet<PartLifecycleTracker> deferredInitializationParts;

            /// <summary>
            /// The managed thread ID of the thread that is currently executing a particular step.
            /// </summary>
            /// <remarks>
            /// This is used to avoid deadlocking when we're executing a particular step
            /// then while executing 3rd party code, that code calls us back on the same thread
            /// to ask for an instance of the part.
            /// In these circumstances we return a partially initialized value rather than
            /// deadlock by trying to wait for a fully initialized one.
            /// This matches MEFv1 and MEFv2 behavior.
            /// </remarks>
            private int? executingStepThreadId;

            /// <summary>
            /// Stores any exception captured during initialization.
            /// </summary>
            private Exception fault;

            /// <summary>
            /// Initializes a new instance of the <see cref="PartLifecycleTracker"/> class.
            /// </summary>
            /// <param name="owningExportProvider">The ExportProvider that owns the lifetime and sharing boundaries for the part to be instantiated.</param>
            /// <param name="sharingBoundary">The sharing boundary the part belongs to.</param>
            public PartLifecycleTracker(ExportProvider owningExportProvider, string sharingBoundary)
            {
                Requires.NotNull(owningExportProvider, nameof(owningExportProvider));

                this.OwningExportProvider = owningExportProvider;
                this.sharingBoundary = sharingBoundary;
                this.deferredInitializationParts = new HashSet<PartLifecycleTracker>();
                this.State = PartLifecycleState.NotCreated;
            }

            /// <summary>
            /// Gets or sets the instantiated part, if applicable and after it has been created. Otherwise <c>null</c>.
            /// </summary>
            public object Value
            {
                get
                {
                    this.ThrowIfDisposed();
                    return this.value;
                }

                set
                {
                    this.value = value;
                }
            }

            /// <summary>
            /// Gets the level of initialization the MEF part has already undergone.
            /// </summary>
            public PartLifecycleState State { get; private set; }

            /// <summary>
            /// Gets the ExportProvider that owns the lifetime and sharing boundaries for the part to be instantiated.
            /// </summary>
            protected ExportProvider OwningExportProvider { get; private set; }

            /// <summary>
            /// Gets the type behind the part.
            /// </summary>
            protected abstract Type PartType { get; }

            /// <summary>
            /// Gets the instance of the part after fully initializing it.
            /// </summary>
            /// <remarks>
            /// In the less common case that this method is called on top of a callstack where this same
            /// part is actually *in progress* of executing any initialization step, this method will
            /// simply return the value as-is rather than deadlock or throw.
            /// This allows certain spec'd MEF behaviors to work.
            /// </remarks>
            public object GetValueReadyToExpose()
            {
                // If this very thread is already executing a step on this part, then we have some
                // form of reentrancy going on. In which case, the general policy seems to be that
                // we return an incompletely initialized part.
                if (this.executingStepThreadId != Environment.CurrentManagedThreadId)
                {
                    this.MoveToState(PartLifecycleState.Final);
                }

                if (this.Value == null)
                {
                    this.ThrowPartNotInstantiableException();
                }

                return this.Value;
            }

            /// <summary>
            /// Gets the instance of the part after instantiating it.
            /// Importing properties may not have been satisfied yet.
            /// </summary>
            public object GetValueReadyToRetrieveExportingMembers()
            {
                this.MoveToState(PartLifecycleState.Created);
                return this.Value;
            }

            /// <summary>
            /// Disposes of the MEF part if it is disposable.
            /// </summary>
            public void Dispose()
            {
                this.isDisposed = true;
                IDisposable disposableValue = this.value as IDisposable;
                this.value = null;
                if (disposableValue != null)
                {
                    disposableValue.Dispose();
                }
            }

            /// <summary>
            /// Instantiates the MEF part and initializes it only so much as executing its importing constructor.
            /// </summary>
            /// <returns>The instantiated MEF part.</returns>
            protected abstract object CreateValue();

            /// <summary>
            /// Satisfies importing members on the MEF part itself.
            /// </summary>
            protected abstract void SatisfyImports();

            /// <summary>
            /// Invokes the OnImportsSatisfied method on the part, if applicable.
            /// </summary>
            /// <remarks>
            /// If not applicable for this MEF part, this method should simply no-op.
            /// </remarks>
            protected abstract void InvokeOnImportsSatisfied();

            /// <summary>
            /// Indicates that a MEF import was satisfied with a value that was not completely initialized
            /// so that it can be initialized later (before this MEF part is allowed to be observed by the MEF client).
            /// </summary>
            /// <param name="importedPart">The part that has been imported by this part without full initialization.</param>
            protected void ReportPartiallyInitializedImport(PartLifecycleTracker importedPart)
            {
                if (importedPart != null)
                {
                    lock (this.syncObject)
                    {
                        this.deferredInitializationParts.Add(importedPart);
                    }
                }
            }

            /// <summary>
            /// Throws a <see cref="CompositionFailedException"/> indicating the part cannot be instantiated.
            /// </summary>
            protected void ThrowPartNotInstantiableException()
            {
                Type partType = this.PartType;
                string partTypeName = partType != null ? partType.FullName : string.Empty;
                throw new CompositionFailedException(string.Format(CultureInfo.CurrentCulture, Strings.PartIsNotInstantiable, partTypeName));
            }

            /// <summary>
            /// Invokes <see cref="CreateValue"/> if this part has not already done so
            /// and performs initial processing of the instance.
            /// </summary>
            private void Create()
            {
                bool creating = false;
                lock (this.syncObject)
                {
                    creating = this.ShouldMoveTo(PartLifecycleState.Creating);
                    if (creating)
                    {
                        this.UpdateState(PartLifecycleState.Creating);
                    }
                }

                Assumes.False(Monitor.IsEntered(this.syncObject)); // Avoid holding locks while calling 3rd party code.
                if (creating)
                {
                    try
                    {
                        this.executingStepThreadId = Environment.CurrentManagedThreadId;
                        object value = this.CreateValue();

                        lock (this.syncObject)
                        {
                            Assumes.True(this.State == PartLifecycleState.Creating);
                            this.Value = value;
                            if (value is IDisposable)
                            {
                                this.OwningExportProvider.TrackDisposableValue(this, this.sharingBoundary);
                            }

                            Assumes.True(this.UpdateState(PartLifecycleState.Created));
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Fault(ex);
                        throw;
                    }
                }
            }

            /// <summary>
            /// Invokes <see cref="SatisfyImports"/> if this part has not already done so.
            /// </summary>
            private void SatisfyImmediateImports()
            {
                // DEADLOCK danger: We could split this up into an in-progress and a complete stage
                // to avoid holding the lock while calling 3rd party code.
                lock (this.syncObject)
                {
                    if (this.ShouldMoveTo(PartLifecycleState.ImmediateImportsSatisfied))
                    {
                        try
                        {
                            this.executingStepThreadId = Environment.CurrentManagedThreadId;
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
            }

            /// <summary>
            /// Invokes <see cref="InvokeOnImportsSatisfied"/> if this part has not already done so.
            /// </summary>
            private void NotifyTransitiveImportsSatisfied()
            {
                try
                {
                    bool shouldInvoke = false;
                    lock (this.syncObject)
                    {
                        // To avoid holding the lock while executing 3rd party code, but still protect
                        // against the instantiated part being exposed too soon, we advance the state
                        // to indicate that we're in progress, then proceed without the lock.
                        shouldInvoke = this.ShouldMoveTo(PartLifecycleState.OnImportsSatisfiedInProgress);
                        if (shouldInvoke)
                        {
                            Assumes.True(this.UpdateState(PartLifecycleState.OnImportsSatisfiedInProgress));
                        }
                    }

                    if (shouldInvoke)
                    {
                        Assumes.False(Monitor.IsEntered(this.syncObject)); // avoid holding locks while invoking others' code.
                        this.executingStepThreadId = Environment.CurrentManagedThreadId;
                        this.InvokeOnImportsSatisfied();

                        Assumes.True(this.UpdateState(PartLifecycleState.OnImportsSatisfiedInvoked));
                    }
                }
                catch (Exception ex)
                {
                    this.Fault(ex);
                    throw;
                }
            }

            /// <summary>
            /// Executes the next step in this part's initialization.
            /// </summary>
            /// <param name="nextState">The state to transition to. It must be no more than one state beyond the current one.</param>
            private void MoveNext(PartLifecycleState nextState)
            {
                Assumes.True(nextState <= this.State + 1, "MoveNext should not be asked to skip a state.");
                switch (nextState)
                {
                    case PartLifecycleState.Creating:
                        this.Create();
                        break;
                    case PartLifecycleState.Created:
                        Verify.Operation(this.executingStepThreadId != Environment.CurrentManagedThreadId, Strings.RecursiveRequestForPartConstruction, this.PartType);

                        // Another thread put this in the Creating state. Just wait for that thread to finish.
                        this.WaitForState(PartLifecycleState.Created);
                        break;
                    case PartLifecycleState.ImmediateImportsSatisfied:
                        this.SatisfyImmediateImports();
                        break;
                    case PartLifecycleState.ImmediateImportsSatisfiedTransitively:
                        this.MoveToStateTransitively(PartLifecycleState.ImmediateImportsSatisfiedTransitively);
                        break;
                    case PartLifecycleState.OnImportsSatisfiedInProgress:
                        this.NotifyTransitiveImportsSatisfied();
                        break;
                    case PartLifecycleState.OnImportsSatisfiedInvoked:
                        // Another thread put this in the OnImportsSatisfiedInProgress state. Just wait for that thread to finish.
                        this.WaitForState(PartLifecycleState.OnImportsSatisfiedInvoked);
                        break;
                    case PartLifecycleState.OnImportsSatisfiedInvokedTransitively:
                        this.MoveToStateTransitively(PartLifecycleState.OnImportsSatisfiedInvokedTransitively);
                        break;
                    case PartLifecycleState.Final:
                        // Nothing to do here. This state is just a marker.
                        this.UpdateState(PartLifecycleState.Final);

                        // Go ahead and free memory now that we're done.
                        this.deferredInitializationParts = null;
                        break;
                    default:
                        throw Assumes.NotReachable();
                }
            }

            /// <summary>
            /// Checks whether the MEF part's next step in initialization is the specified one.
            /// </summary>
            /// <param name="nextState">The step that is expected to be the next appropriate one.</param>
            /// <returns>
            /// <c>true</c> if <paramref name="nextState"/> is one step beyond the current <see cref="State"/>.
            /// <c>false</c> if this MEF part has advanced to or beyond that step already.
            /// </returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown if this part is not yet ready for this step because that is a sign of a bug in the caller.
            /// </exception>
            private bool ShouldMoveTo(PartLifecycleState nextState)
            {
                lock (this.syncObject)
                {
                    this.ThrowIfFaulted();

                    if (this.State >= nextState)
                    {
                        return false;
                    }

                    if (this.State < nextState - 1)
                    {
                        Verify.FailOperation(Strings.UnexpectedSharedPartState, this.State, nextState - 1);
                    }

                    return true;
                }
            }

            /// <summary>
            /// Advances this MEF part to the specified stage of initialization.
            /// </summary>
            /// <param name="requiredState">The initialization state to advance to.</param>
            /// <remarks>
            /// If the specified state has already been reached, this method simply returns to the caller.
            /// </remarks>
            private void MoveToState(PartLifecycleState requiredState)
            {
                this.ThrowIfFaulted();

                PartLifecycleState state;
                while ((state = this.State) < requiredState)
                {
                    this.MoveNext(state + 1);
                    this.ThrowIfFaulted();
                }
            }

            /// <summary>
            /// Advances this part and everything it imports (transitively) to the specified state.
            /// </summary>
            /// <param name="requiredState">The state to advance this and all related parts to.</param>
            /// <param name="visitedNodes">
            /// Used in the recursive call to avoid loops leading to stack overflows.
            /// It also identifies all related parts so they can be "stamped" as being transitively initialized.
            /// This MUST be <c>null</c> for non-recursive calls.
            /// </param>
            private void MoveToStateTransitively(PartLifecycleState requiredState, HashSet<PartLifecycleTracker> visitedNodes = null)
            {
                try
                {
                    bool topLevelCall = visitedNodes == null;
                    PartLifecycleState nonTransitiveState = topLevelCall ? requiredState - 1 : requiredState;
                    PartLifecycleState transitiveState = topLevelCall ? requiredState : requiredState + 1;

                    // Short circuit a recursive walk through a potentially large graph by skipping this node
                    // and others it points to when this node has already advanced to this *transitive* state.
                    // If we were to skip for already being at merely the non-transitive state, we'd be
                    // inappropriately skipping other nodes in the graph that may not be to this level yet,
                    // and visitedNodes would not be updated either.
                    if (this.State >= transitiveState)
                    {
                        return;
                    }

                    this.MoveToState(nonTransitiveState);
                    visitedNodes = visitedNodes ?? new HashSet<PartLifecycleTracker>();
                    if (visitedNodes.Add(this))
                    {
                        // This code may execute on this instance from multiple threads concurrently.
                        // We're not holding a lock, but each individual method we're calling is thread-safe.
                        // Enumerating the set of deferred initialization parts is inherently thread-safe as a read,
                        // and all writes to that collection have concluded already for us to reach this point.
                        // However we might race with another thread that *clears* the field entirely as this
                        // transitions to its Final state on that other thread. So defend against that.
                        // If we capture the field value and then enumerate over that captured collection after
                        // another thread clears the field, that's not a problem. The loop will ultimately no-op.
                        var deferredInitializationParts = this.deferredInitializationParts;
                        if (deferredInitializationParts != null)
                        {
                            foreach (var importedPart in deferredInitializationParts)
                            {
                                // Do not ask these parts to mark themselves as transitively complete.
                                // Only the top-level call can know when everyone is transitively done.
                                importedPart.MoveToStateTransitively(nonTransitiveState, visitedNodes);
                            }
                        }

                        if (topLevelCall)
                        {
                            // Update everyone involved so they know they're transitively done with this work.
                            foreach (var importedPart in visitedNodes)
                            {
                                importedPart.UpdateState(transitiveState);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Fault(ex);
                    throw;
                }
            }

            /// <summary>
            /// Indicates that a new stage of initialization has been reached.
            /// </summary>
            /// <param name="newState">The new state.</param>
            /// <returns><c>true</c> if the new state actually represents an advancement over the prior state.</returns>
            private bool UpdateState(PartLifecycleState newState)
            {
                lock (this.syncObject)
                {
                    if (this.State < newState)
                    {
                        this.State = newState;
                        this.executingStepThreadId = null;

                        // Alert any other threads waiting for this (see the WaitForState method).
                        Monitor.PulseAll(this.syncObject);
                        return true;
                    }

                    return false;
                }
            }

            /// <summary>
            /// Blocks the calling thread until this Part reaches the required initialization stage.
            /// </summary>
            /// <param name="state">The stage required by the caller.</param>
            private void WaitForState(PartLifecycleState state)
            {
                lock (this.syncObject)
                {
                    // Keep sleeping until the state reaches the one required by our caller.
                    while (this.State < state)
                    {
                        // Monitor.Wait releases the lock and sleeps until someone holding the lock calls Monitor.PulseAll.
                        // We sleep with timeout for reasons given below. But we keep waiting again until we actually are pulsed.
                        while (!Monitor.Wait(this.syncObject, TimeSpan.FromSeconds(3)))
                        {
                            // This area intentionally left blank. It exists so that managed debuggers
                            // can break out of a hang temporarily to get out of optimized/native frames
                            // on the top of the stack so the debugger can actually be useful.
                        }
                    }
                }
            }

            /// <summary>
            /// Rethrows an exception experienced while initializing this MEF part if there is one.
            /// </summary>
            private void ThrowIfFaulted()
            {
                if (this.fault != null)
                {
                    ExceptionDispatchInfo.Capture(this.fault).Throw();
                }
            }

            /// <summary>
            /// Throw an <see cref="ObjectDisposedException"/> if this instance has been disposed of previously.
            /// </summary>
            private void ThrowIfDisposed()
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
            }

            /// <summary>
            /// Records that a failure occurred in initializing this part
            /// and advances this Part to its <see cref="PartLifecycleState.Final"/>.
            /// </summary>
            /// <param name="exception">The failure.</param>
            private void Fault(Exception exception)
            {
                lock (this.syncObject)
                {
                    if (this.fault != exception)
                    {
                        Report.If(this.fault != null, "We shouldn't have faulted twice in a row. The first should have done us in.");
                        if (exception != null)
                        {
                            this.fault = exception;
                        }
                    }

                    this.UpdateState(PartLifecycleState.Final);
                }
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
                throw new InvalidOperationException(Strings.CannotDirectlyDisposeAnImport);
            }
        }
    }
}
