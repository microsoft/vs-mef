// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class ContainerIsolationTests
    {
        private readonly ITestOutputHelper output;

        public ContainerIsolationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task TwoContainersDoNotShareAnyExports()
        {
            var discovery = TestUtilities.V2Discovery;
            var part = discovery.CreatePart(typeof(SharedExport))!;
            var catalog = TestUtilities.EmptyCatalog.AddPart(part);
            var configuration = CompositionConfiguration.Create(catalog);
            var container1 = await configuration.CreateContainerAsync(this.output);
            var container2 = await configuration.CreateContainerAsync(this.output);

            var export1 = container1.GetExportedValue<SharedExport>();
            var export2 = container2.GetExportedValue<SharedExport>();
            Assert.NotSame(export1, export2);
        }

        [Export, Shared]
        public class SharedExport { }
    }
}
