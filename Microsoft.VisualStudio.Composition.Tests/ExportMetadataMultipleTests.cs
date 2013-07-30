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

    [Trait("Metadata", "Multiple")]
    public class ExportMetadataMultipleTests
    {
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void MultipleExportMetadataValuesForOneKey(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            object metadataValue = importingPart.ImportingProperty.Metadata["SomeName"];
            Assert.IsType<string[]>(metadataValue);
            Assert.Equal(2, ((string[])metadataValue).Length);
            Assert.Contains("b", (string[])metadataValue);
            Assert.Contains("c", (string[])metadataValue);
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void MultipleCustomExportMetadataValuesForOneKey(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            object metadataValue = importingPart.CustomMetadataImport.Metadata["SomeName"];
            Assert.IsType<string[]>(metadataValue);
            Assert.Equal(2, ((string[])metadataValue).Length);
            Assert.Contains("b", (string[])metadataValue);
            Assert.Contains("c", (string[])metadataValue);
        }

        [Export]
        [ExportMetadata("SomeName", "b")]
        [ExportMetadata("SomeName", "c")]
        [MefV1.Export]
        [MefV1.ExportMetadata("SomeName", "b", IsMultiple = true)]
        [MefV1.ExportMetadata("SomeName", "c", IsMultiple = true)]
        public class PartWithMultipleMetadata { }

        [Export]
        [MefV1.Export]
        [CustomMetadata(SomeName = "b")]
        [CustomMetadata(SomeName = "c")]
        public class PartWithMultipleCustomMetadata { }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import]
            [MefV1.Import]
            public Lazy<PartWithMultipleMetadata, IDictionary<string, object>> ImportingProperty { get; set; }

            [Import]
            [MefV1.Import]
            public Lazy<PartWithMultipleCustomMetadata, IDictionary<string, object>> CustomMetadataImport { get; set; }
        }

        [MetadataAttribute, MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
        public class CustomMetadataAttribute : Attribute
        {
            public string SomeName { get; set; }
        }
    }
}
