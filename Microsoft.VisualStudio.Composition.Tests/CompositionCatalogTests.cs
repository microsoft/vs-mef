namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MEFv1 = System.ComponentModel.Composition;

    public class CompositionCatalogTests
    {
        [Fact]
        public void CreateFromTypesOmitsNonPartsV1()
        {
            var catalog = ComposableCatalog.Create(new AttributedPartDiscoveryV1(), new[] { typeof(NonExportingType), typeof(ExportingType) });
            Assert.Equal(1, catalog.Parts.Count);
            Assert.Equal(typeof(ExportingType), catalog.Parts.Single().Type);
        }

        [Fact]
        public void CreateFromTypesOmitsNonPartsV2()
        {
            var catalog = ComposableCatalog.Create(new AttributedPartDiscovery(), new[] { typeof(NonExportingType), typeof(ExportingType) });
            Assert.Equal(1, catalog.Parts.Count);
            Assert.Equal(typeof(ExportingType), catalog.Parts.Single().Type);
        }

        [Fact]
        public void WithPartNullThrows()
        {
            var catalog = ComposableCatalog.Create();
            Assert.Throws<ArgumentNullException>(() => catalog.WithPart(null));
        }

        public class NonExportingType { }

        [Export, MEFv1.Export]
        public class ExportingType { }
    }
}
