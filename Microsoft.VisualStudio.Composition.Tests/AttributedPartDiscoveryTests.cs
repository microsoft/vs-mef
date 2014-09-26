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
        private AttributedPartDiscovery discovery = new AttributedPartDiscovery();

        protected override PartDiscovery DiscoveryService
        {
            get { return this.discovery; }
        }

        [Fact]
        public void MissingImportingConstructor()
        {
            Assert.Throws<InvalidOperationException>(() => this.DiscoveryService.CreatePart(typeof(SomePartWithoutImportingConstructor)));
        }
    }
}
