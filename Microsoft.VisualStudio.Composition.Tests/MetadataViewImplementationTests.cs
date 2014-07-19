namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class MetadataViewImplementationTests
    {
        [MefFact(CompositionEngines.V1)]
        public void MetadataViewImplementationDirectQuery(IContainer container)
        {
            var export = container.GetExport<ExportingPart, IMetadataView>();
            Assert.IsType<MetadataViewClass>(export.Metadata);
            Assert.Equal("1", export.Metadata.A);
            Assert.Null(export.Metadata.B);
        }

        [MefFact(CompositionEngines.V1)]
        public void MetadataViewImplementationViaImport(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            Assert.IsType<MetadataViewClass>(importingPart.ImportingProperty.Metadata);
            Assert.Equal("1", importingPart.ImportingProperty.Metadata.A);
            Assert.Null(importingPart.ImportingProperty.Metadata.B);
        }

        [MefV1.Export]
        public class ImportingPart
        {
            [MefV1.Import]
            public Lazy<ExportingPart, IMetadataView> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        [MefV1.ExportMetadata("A", "1")]
        public class ExportingPart { }

        [MefV1.MetadataViewImplementation(typeof(MetadataViewClass))]
        public interface IMetadataView
        {
            string A { get; }

            [DefaultValue("default")]
            string B { get; }
        }

        public class MetadataViewClass : IMetadataView
        {
            public MetadataViewClass(IDictionary<string, object> metadata)
            {
                this.A = (string)(metadata.ContainsKey("A") ? metadata["A"] : null);
                this.B = (string)(metadata.ContainsKey("B") ? metadata["B"] : null);
            }

            public string A { get; set; }

            public string B { get; set; }
        }
    }
}
