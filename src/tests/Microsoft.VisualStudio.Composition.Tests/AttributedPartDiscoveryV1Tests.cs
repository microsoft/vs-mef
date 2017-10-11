// Copyright (c) Microsoft. All rights reserved.

#if DESKTOP

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class AttributedPartDiscoveryV1Tests : AttributedPartDiscoveryTestBase
    {
        protected override PartDiscovery DiscoveryService
        {
            get { return TestUtilities.V1Discovery; }
        }

        [Fact]
        public void MissingImportingConstructor()
        {
            var part = this.DiscoveryService.CreatePart(typeof(SomePartWithoutImportingConstructor));
            Assert.NotNull(part);
            Assert.False(part.IsInstantiable);
        }
    }
}

#endif
