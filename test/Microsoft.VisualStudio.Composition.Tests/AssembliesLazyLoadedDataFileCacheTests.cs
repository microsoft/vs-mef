// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class AssembliesLazyLoadedDataFileCacheTests : AssembliesLazyLoadedTests
    {
        public AssembliesLazyLoadedDataFileCacheTests()
            : base(new CachedComposition())
        {
        }
    }
}

#endif
