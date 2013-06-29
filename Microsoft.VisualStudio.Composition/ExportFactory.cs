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
            return (T)this.GetExport(typeof(T));
        }

        protected abstract object GetExport(Type type);
    }
}
