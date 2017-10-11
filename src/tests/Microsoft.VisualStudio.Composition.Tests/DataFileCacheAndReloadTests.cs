// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class DataFileCacheAndReloadTests : CacheAndReloadTests
    {
        public DataFileCacheAndReloadTests(ITestOutputHelper logger)
            : base(logger, new CachedComposition())
        {
        }
    }
}
