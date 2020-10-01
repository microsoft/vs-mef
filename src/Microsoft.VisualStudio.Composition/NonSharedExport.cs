// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;

    internal class NonSharedExport : Export, IDisposable
    {
        internal NonSharedExport(ExportDefinition definition, Func<object> exportedValueGetter)
            : base(definition, exportedValueGetter)
        {
        }

        internal NonSharedExport(ExportDefinition definition, Lazy<object> exportedValueGetter)
            : base(definition, exportedValueGetter)
        {
        }

        internal NonSharedExport(string contractName, IReadOnlyDictionary<string, object> metadata, Func<object> exportedValueGetter)
            : base(contractName, metadata, exportedValueGetter)
        {
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
