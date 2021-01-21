// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
