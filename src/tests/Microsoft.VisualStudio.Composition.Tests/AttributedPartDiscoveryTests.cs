// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;

    public class AttributedPartDiscoveryTests : AttributedPartDiscoveryTestBase
    {
        protected override PartDiscovery DiscoveryService
        {
            get { return TestUtilities.V2DiscoveryWithNonPublics; }
        }

        [Fact]
        public void MissingImportingConstructor()
        {
            Assert.Throws<InvalidOperationException>(() => this.DiscoveryService.CreatePart(typeof(SomePartWithoutImportingConstructor)));
        }
    }
}
