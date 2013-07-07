namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public abstract class ExportProvider
    {
        private readonly object syncObject = new object();

        /// <summary>
        /// A dictionary of types to their Lazy{T} factories.
        /// </summary>
        private Dictionary<Type, object> sharedInstantiatedExports = new Dictionary<Type, object>();

        public ILazy<T> GetExport<T>()
        {
            return this.GetExport<T>(null);
        }

        public ILazy<T> GetExport<T>(string contractName)
        {
            var exportDefinition = new ExportDefinition(new CompositionContract(contractName, typeof(T)));
            return (ILazy<T>)this.GetExport(exportDefinition);
        }

        public T GetExportedValue<T>()
        {
            return this.GetExport<T>().Value;
        }

        public T GetExportedValue<T>(string contractName)
        {
            return this.GetExport<T>(contractName).Value;
        }

        /// <summary>
        /// When implemented by a derived class, returns an <see cref="ILazy&lt;T&gt;"/> value that
        /// satisfies the specified <see cref="ExportDefinition"/>.
        /// </summary>
        protected abstract object GetExport(ExportDefinition exportDefinition);

        protected bool TryGetSharedInstanceFactory<T>(out ILazy<T> value)
        {
            lock (this.syncObject)
            {
                object valueObject;
                bool result = this.sharedInstantiatedExports.TryGetValue(typeof(T), out valueObject);
                value = (ILazy<T>)valueObject;
                return result;
            }
        }

        protected ILazy<T> GetOrAddSharedInstanceFactory<T>(ILazy<T> value)
        {
            Requires.NotNull(value, "value");

            lock (this.syncObject)
            {
                object priorValue;
                if (this.sharedInstantiatedExports.TryGetValue(typeof(T), out priorValue))
                {
                    return (ILazy<T>)priorValue;
                }

                this.sharedInstantiatedExports.Add(typeof(T), value);
                return value;
            }
        }
    }
}
