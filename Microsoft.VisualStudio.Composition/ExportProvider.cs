namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public abstract class ExportProvider : IDisposable
    {
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
        }

        public ILazy<T> GetExport<T>()
        {
            return this.GetExport<T>(null);
        }

        public ILazy<T> GetExport<T>(string contractName)
        {
            var exportDefinition = new ExportDefinition(new CompositionContract(contractName, typeof(T)), ImmutableDictionary.Create<string, object>());
            return (ILazy<T>)this.GetExport(exportDefinition);
        }

        public ILazy<T, TMetadataView> GetExport<T, TMetadataView>()
        {
            return this.GetExport<T, TMetadataView>(null);
        }

        public ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
        {
            throw new NotImplementedException();
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
            var exportDefinition = new ExportDefinition(new CompositionContract(contractName, typeof(T)), ImmutableDictionary.Create<string, object>());
            return (IEnumerable<ILazy<T>>)this.GetExports(exportDefinition);
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
        {
            return this.GetExports<T, TMetadataView>(null);
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetExportedValues<T>()
        {
            return this.GetExports<T>().Select(l => l.Value);
        }

        public IEnumerable<T> GetExportedValues<T>(string contractName)
        {
            return this.GetExports<T>(contractName).Select(l => l.Value);
        }

        public void Dispose()
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

        protected static object CannotInstantiatePartWithNoImportingConstructor()
        {
            throw new System.ComponentModel.Composition.CompositionException("No importing constructor");
        }

        /// <summary>
        /// When implemented by a derived class, returns an <see cref="ILazy&lt;T&gt;"/> value that
        /// satisfies the specified <see cref="ExportDefinition"/>.
        /// </summary>
        protected abstract object GetExport(ExportDefinition exportDefinition);

        /// <summary>
        /// When implemented by a derived class, returns an <see cref="IEnumerable&lt;ILazy&lt;T&gt;&gt;"/> value that
        /// satisfies the specified <see cref="ExportDefinition"/>.
        /// </summary>
        protected abstract IEnumerable<object> GetExports(ExportDefinition exportDefinition);

        protected bool TryGetSharedInstanceFactory<T>(string partSharingBoundary, Type type, out ILazy<T> value)
        {
            Requires.NotNull(type, "type");
            Assumes.True(typeof(T).IsAssignableFrom(type));

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
            Assumes.True(typeof(T).IsAssignableFrom(type));

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

        private Dictionary<Type, object> AcquireSharingBoundaryInstances(string sharingBoundaryName)
        {
            Requires.NotNull(sharingBoundaryName, "sharingBoundaryName");

            // If this throws an IndexOutOfRangeException, it means someone is trying to create a part
            // that belongs to a sharing boundary that has not yet been created.
            var sharingBoundary = this.sharedInstantiatedExports[sharingBoundaryName];
            return sharingBoundary;
        }
    }
}
