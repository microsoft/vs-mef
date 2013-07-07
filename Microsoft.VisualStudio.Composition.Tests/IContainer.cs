namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IContainer : IDisposable
    {
        T GetExportedValue<T>();

        T GetExportedValue<T>(string contractName);

        ILazy<T> GetExport<T>();

        ILazy<T> GetExport<T>(string contractName);
    }
}
