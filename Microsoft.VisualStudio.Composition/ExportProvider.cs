namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using DefaultMetadataType = System.Collections.Generic.IDictionary<string, object>;

    public abstract class ExportProvider : IDisposable
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

        private readonly object syncObject = new object();

        /// <summary>
        /// A map of shared boundary names to their shared instances.
        /// The value is a dictionary of types to their Lazy{T} factories.
        /// </summary>
        private readonly ImmutableDictionary<string, Dictionary<Type, object>> sharedInstantiatedExports = ImmutableDictionary.Create<string, Dictionary<Type, object>>();

        /// <summary>
        /// The disposable objects whose lifetimes are controlled by this instance.
        /// </summary>
        private readonly HashSet<IDisposable> disposableInstantiatedParts = new HashSet<IDisposable>();

        protected ExportProvider(ExportProvider parent, string[] freshSharingBoundaries)
        {
            if (parent == null)
            {
                this.sharedInstantiatedExports = this.sharedInstantiatedExports.Add(string.Empty, new Dictionary<Type, object>());
            }
            else
            {
                this.sharedInstantiatedExports = parent.sharedInstantiatedExports;
            }

            if (freshSharingBoundaries != null)
            {
                foreach (string freshSharingBoundary in freshSharingBoundaries)
                {
                    this.sharedInstantiatedExports = this.sharedInstantiatedExports.SetItem(freshSharingBoundary, new Dictionary<Type, object>());
                }
            }

            var nonDisposableWrapper = this is ExportProviderAsExport ? this : new ExportProviderAsExport(this, null, null);
            this.NonDisposableWrapper = LazyPart.Wrap(nonDisposableWrapper);
        }

        protected ILazy<ExportProvider> NonDisposableWrapper { get; private set; }

        public ILazy<T> GetExport<T>()
        {
            return this.GetExport<T>(null);
        }

        public ILazy<T> GetExport<T>(string contractName)
        {
            return this.GetExport<T, DefaultMetadataType>(contractName);
        }

        public ILazy<T, TMetadataView> GetExport<T, TMetadataView>()
        {
            return this.GetExport<T, TMetadataView>(null);
        }

        public ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
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

        public IEnumerable<ILazy<T>> GetExports<T>()
        {
            return this.GetExports<T>(null);
        }

        public IEnumerable<ILazy<T>> GetExports<T>(string contractName)
        {
            return this.GetExports<T, DefaultMetadataType>(contractName);
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
        {
            return this.GetExports<T, TMetadataView>(null);
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
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

        public IEnumerable<Export> GetExports(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            IEnumerable<Export> exports = this.GetExportsCore(importDefinition);

            string genericTypeDefinitionContractName;
            Type[] genericTypeArguments;
            if (ComposableCatalog.TryGetOpenGenericExport(importDefinition, out genericTypeDefinitionContractName, out genericTypeArguments))
            {
                var genericTypeImportDefinition = new ImportDefinition(genericTypeDefinitionContractName, importDefinition.Cardinality, importDefinition.Metadata, importDefinition.ExportContraints);
                var openGenericExports = this.GetExportsCore(genericTypeImportDefinition);
                var closedGenericExports = openGenericExports.Select(export => export.CloseGenericExport(genericTypeArguments));
                exports = exports.Concat(closedGenericExports);
            }

            var filteredExports = from export in exports
                                  where importDefinition.ExportContraints.All(c => c.IsSatisfiedBy(export.Definition))
                                  select export;
            if (importDefinition.Cardinality == ImportCardinality.ExactlyOne && filteredExports.Count() != 1)
            {
                throw new CompositionFailedException();
            }

            return filteredExports;
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

        protected bool TryGetSharedInstanceFactory<T>(string partSharingBoundary, Type type, out ILazy<T> value)
        {
            Requires.NotNull(type, "type");
            Assumes.True(typeof(T).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()));

            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                object valueObject;
                bool result = sharingBoundary.TryGetValue(type, out valueObject);
                value = (ILazy<T>)valueObject;
                return result;
            }
        }

        protected ILazy<T> GetOrAddSharedInstanceFactory<T>(string partSharingBoundary, Type type, ILazy<T> value)
        {
            Requires.NotNull(type, "type");
            Requires.NotNull(value, "value");
            Assumes.True(typeof(T).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()));

            lock (this.syncObject)
            {
                var sharingBoundary = AcquireSharingBoundaryInstances(partSharingBoundary);
                object priorValue;
                if (sharingBoundary.TryGetValue(type, out priorValue))
                {
                    return (ILazy<T>)priorValue;
                }

                sharingBoundary.Add(type, value);
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

        private IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName, ImportCardinality cardinality)
        {
            contractName = string.IsNullOrEmpty(contractName) ? ContractNameServices.GetTypeIdentity(typeof(T)) : contractName;

            var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty
                .Add(new ExportTypeIdentityConstraint(typeof(T)));

            if (typeof(TMetadataView) != typeof(DefaultMetadataType))
            {
                constraints = constraints.Add(new ImportMetadataViewConstraint(typeof(TMetadataView)));
            }

            var importMetadata = PartDiscovery.GetImportMetadataForGenericTypeImport(typeof(T));
            var importDefinition = new ImportDefinition(contractName, cardinality, importMetadata, constraints);
            IEnumerable<Export> results = this.GetExports(importDefinition);
            return results.Select(result => new LazyPart<T, TMetadataView>(() => result.Value, (TMetadataView)result.Metadata));
        }

        private Dictionary<Type, object> AcquireSharingBoundaryInstances(string sharingBoundaryName)
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

        private class ExportProviderAsExport : ExportProvider
        {
            private readonly ExportProvider inner;

            internal ExportProviderAsExport(ExportProvider inner, ExportProvider parent, string[] freshSharingBoundaries)
                : base(parent, freshSharingBoundaries)
            {
                Requires.NotNull(inner, "inner");

                this.inner = inner;
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition)
            {
                return this.inner.GetExportsCore(definition);
            }

            protected override void Dispose(bool disposing)
            {
                throw new InvalidOperationException("This instance is an import and cannot be directly disposed.");
            }
        }
    }
}
