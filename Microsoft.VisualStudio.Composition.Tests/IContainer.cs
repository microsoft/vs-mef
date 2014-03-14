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

        IEnumerable<T> GetExportedValues<T>();

        IEnumerable<T> GetExportedValues<T>(string contractName);

        ILazy<T> GetExport<T>();

        ILazy<T> GetExport<T>(string contractName);

        ILazy<T, TMetadataView> GetExport<T, TMetadataView>();

        ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName);

        IEnumerable<ILazy<T>> GetExports<T>();

        IEnumerable<ILazy<T>> GetExports<T>(string contractName);

        IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>();

        IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName);
    }
}
