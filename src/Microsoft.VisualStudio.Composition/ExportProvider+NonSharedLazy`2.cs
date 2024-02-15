// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Reflection;

    public partial class ExportProvider
    {
        private interface INonSharedLazy
        {
            NonSharedExport NonSharedExport { get; }
        }

        private class NonSharedLazy<T, TMetadata> : Lazy<T, TMetadata>, INonSharedLazy, IComposedLazy
        {
            internal NonSharedLazy(Func<T> valueFactory, TMetadata metadata, NonSharedExport chainDisposable, AssemblyName? assemblyName)
                : base(valueFactory, metadata)
            {
                Requires.NotNull(chainDisposable, nameof(chainDisposable));
                this.NonSharedExport = chainDisposable;
                this.AssemblyName = assemblyName;
            }

            public NonSharedExport NonSharedExport { get; }

            public AssemblyName? AssemblyName { get; }
        }
    }
}
