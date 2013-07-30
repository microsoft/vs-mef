namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Metadata", "NonStringValues")]
    public class ExportMetadataNonStringValuesTests
    {
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void ExportMetadataWithIntValues(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            object metadataValue = importingPart.ImportingProperty.Metadata["a"];
            Assert.IsType<int>(metadataValue);
            Assert.Equal(5, metadataValue);
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("a", 5)]
        [MefV1.ExportMetadata("a", 5)]
        public class PartWithIntMetadataValues { }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import]
            [MefV1.Import]
            public Lazy<PartWithIntMetadataValues, IDictionary<string, object>> ImportingProperty { get; set; }
        }
    }
}
