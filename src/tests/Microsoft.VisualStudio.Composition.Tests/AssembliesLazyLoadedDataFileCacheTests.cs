// Copyright (c) Microsoft. All rights reserved.

#if DESKTOP

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
