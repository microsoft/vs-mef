namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public abstract class ExportProvider : IDisposable
    {
        internal static readonly CompositionContract ExportProviderContract = new CompositionContract(null, typeof(ExportProvider));

        internal static readonly ComposablePartDefinition ExportProviderPartDefinition = new ComposablePartDefinition(
            typeof(ExportProviderAsExport),
            new[] { new ExportDefinition(ExportProviderContract, PartCreationPolicyConstraint.GetExportMetadata(CreationPolicy.Shared)) },
            ImmutableDictionary<MemberInfo, IReadOnlyList<ExportDefinition>>.Empty,
            ImmutableDictionary<MemberInfo, ImportDefinition>.Empty,
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
            var compositionContract = new CompositionContract(contractName, typeof(T));
            return (ILazy<T>)ExpectOne(this.GetExports(compositionContract));
        }

        public ILazy<T, TMetadataView> GetExport<T, TMetadataView>()
        {
            return this.GetExport<T, TMetadataView>(null);
        }

        public ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
        {
            var compositionContract = new CompositionContract(contractName, typeof(T));
            var result = (ILazy<T, TMetadataView>)ExpectOne(this.GetExports(compositionContract));
            if (result == null)
            {
                throw new CompositionFailedException();
            }

            return result;
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
            var compositionContract = new CompositionContract(contractName, typeof(T));
            var result = (IEnumerable<ILazy<T>>)this.GetExports(compositionContract) ?? Enumerable.Empty<ILazy<T>>();
            return result;
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
        {
            return this.GetExports<T, TMetadataView>(null);
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
        {
            var compositionContract = new CompositionContract(contractName, typeof(T));
            var result = (IEnumerable<ILazy<T, TMetadataView>>)this.GetExports(compositionContract);
            if (result == null)
            {
                return Enumerable.Empty<ILazy<T, TMetadataView>>();
            }

            return result;
        }

        public IEnumerable<T> GetExportedValues<T>()
        {
            return this.GetExports<T>().Select(l => l.Value);
        }

        public IEnumerable<T> GetExportedValues<T>(string contractName)
        {
            return this.GetExports<T>(contractName).Select(l => l.Value);
        }

        public IEnumerable<ILazy<object>> GetExports(Type contractType, string contractName)
        {
            Requires.NotNull(contractType, "contractType");

            var compositionContract = new CompositionContract(contractName, contractType);
            var result = (IEnumerable<ILazy<object>>)this.GetExports(compositionContract) ?? Enumerable.Empty<ILazy<object>>();
            return result;
        }

        public IEnumerable<ILazy<object, IDictionary<string, object>>> GetExports(string contractName)
        {
            throw new NotImplementedException();
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
        /// satisfy the specified <see cref="ExportDefinition"/>.
        /// </summary>
        protected abstract IEnumerable<object> GetExports(CompositionContract compositionContract);

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

        private static object ExpectOne(IEnumerable<object> exports)
        {
            if (exports == null)
            {
                // Null is our derived type's way of saying there are no exports.
                throw new CompositionFailedException();
            }

            try
            {
                return exports.Single();
            }
            catch (InvalidOperationException)
            {
                throw new CompositionFailedException();
            }
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

            protected override IEnumerable<object> GetExports(CompositionContract compositionContract)
            {
                return this.inner.GetExports(compositionContract);
            }

            protected override void Dispose(bool disposing)
            {
                throw new InvalidOperationException("This instance is an import and cannot be directly disposed.");
            }
        }
    }
}
