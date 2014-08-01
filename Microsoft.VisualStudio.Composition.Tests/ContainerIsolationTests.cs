namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ContainerIsolationTests
    {
        [Fact]
        public void TwoContainersDoNotShareAnyExports()
        {
            var discovery = new AttributedPartDiscovery();
            var part = discovery.CreatePart(typeof(SharedExport));
            var configuration = CompositionConfiguration.Create(new[] { part });
            var container1 = configuration.CreateContainer(true);
            var container2 = configuration.CreateContainer(true);

            var export1 = container1.GetExportedValue<SharedExport>();
            var export2 = container2.GetExportedValue<SharedExport>();
            Assert.NotSame(export1, export2);
        }

        [Export, Shared]
        public class SharedExport { }
    }
}
