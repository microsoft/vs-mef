namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public abstract class ExportFactory
    {
        private readonly object syncObject = new object();

        /// <summary>
        /// A dictionary of types to their Lazy{T} factories.
        /// </summary>
        private Dictionary<Type, object> sharedInstantiatedExports = new Dictionary<Type, object>();

        public T GetExport<T>() where T : class
        {
            return this.GetExport<T>(null);
        }

        public T GetExport<T>(string contractName) where T : class
        {
            var exportDefinition = new ExportDefinition(new CompositionContract(contractName, typeof(T)));
            return (T)this.GetExport(exportDefinition);
        }

        protected abstract object GetExport(ExportDefinition exportDefinition);

        protected bool TryGetSharedInstanceFactory<T>(out Lazy<T> value)
        {
            lock (this.syncObject)
            {
                object valueObject;
                bool result = this.sharedInstantiatedExports.TryGetValue(typeof(T), out valueObject);
                value = (Lazy<T>)valueObject;
                return result;
            }
        }

        protected Lazy<T> GetOrAddSharedInstanceFactory<T>(Lazy<T> value)
        {
            Requires.NotNull(value, "value");

            lock (this.syncObject)
            {
                object priorValue;
                if (this.sharedInstantiatedExports.TryGetValue(typeof(T), out priorValue))
                {
                    return (Lazy<T>)priorValue;
                }

                this.sharedInstantiatedExports.Add(typeof(T), value);
                return value;
            }
        }
    }
}
