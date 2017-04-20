// Copyright (c) Microsoft. All rights reserved.

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

        Lazy<T> GetExport<T>();

        Lazy<T> GetExport<T>(string contractName);

        Lazy<T, TMetadataView> GetExport<T, TMetadataView>();

        Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName);

        IEnumerable<Lazy<T>> GetExports<T>();

        IEnumerable<Lazy<T>> GetExports<T>(string contractName);

        IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>();

        IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName);
    }
}
