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
            var configuration = CompositionConfiguration.Create(typeof(SharedExport));
            var container1 = configuration.CreateContainer();
            var container2 = configuration.CreateContainer();

            var export1 = container1.GetExport<SharedExport>();
            var export2 = container2.GetExport<SharedExport>();
            Assert.NotSame(export1, export2);
        }

        [Export, Shared]
        public class SharedExport { }
    }
}
