// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;

    public interface IContainer : IDisposable
    {
        T GetExportedValue<T>();

        T GetExportedValue<T>(string? contractName);

        IEnumerable<T> GetExportedValues<T>();

        IEnumerable<T> GetExportedValues<T>(string? contractName);

        IEnumerable<object?> GetExportedValues(Type type, string? contractName);

        Lazy<T> GetExport<T>();

        Lazy<T> GetExport<T>(string? contractName);

        Lazy<T, TMetadataView> GetExport<T, TMetadataView>();

        Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string? contractName);

        IEnumerable<Lazy<T>> GetExports<T>();

        IEnumerable<Lazy<T>> GetExports<T>(string? contractName);

        IEnumerable<Lazy<object?, object>> GetExports(Type type, Type metadataViewType, string? contractName);

        IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>();

        IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string? contractName);

        void ReleaseExport<T>(Lazy<T> export);

        void ReleaseExports<T>(IEnumerable<Lazy<T>> export);

        void ReleaseExports<T, TMetadataView>(IEnumerable<Lazy<T, TMetadataView>> export);
    }
}
