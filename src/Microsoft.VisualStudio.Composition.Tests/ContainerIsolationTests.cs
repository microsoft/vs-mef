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
            var discovery = new AttributedPartDiscovery();
            var part = discovery.CreatePart(typeof(SharedExport));
            var configuration = CompositionConfiguration.Create(new[] { part });
            var container1 = await configuration.CreateContainerAsync(true, this.output);
            var container2 = await configuration.CreateContainerAsync(true, this.output);

            var export1 = container1.GetExportedValue<SharedExport>();
            var export2 = container2.GetExportedValue<SharedExport>();
            Assert.NotSame(export1, export2);
        }

        [Export, Shared]
        public class SharedExport { }
    }
}
