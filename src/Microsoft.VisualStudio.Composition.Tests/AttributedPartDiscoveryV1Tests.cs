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
        private AttributedPartDiscoveryV1 discovery = new AttributedPartDiscoveryV1();

        protected override PartDiscovery DiscoveryService
        {
            get { return this.discovery; }
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
