namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class ExportFactory
    {
        public abstract T GetExport<T>() where T : class;
    }
}
