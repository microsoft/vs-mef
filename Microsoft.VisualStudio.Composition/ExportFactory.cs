namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class ExportFactory
    {
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
    }
}
