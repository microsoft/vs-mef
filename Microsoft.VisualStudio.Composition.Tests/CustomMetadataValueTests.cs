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

    [Trait("Metadata", "CustomValue")]
    public class CustomMetadataValueTests
    {
        [MefFact(CompositionEngines.V1, InvalidConfiguration = true)]
        public void CustomMetadataValueV1(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPart>();
            Assert.IsType<MyObjectType>(importer.ImportingProperty.Metadata["SomeName"]);
        }

        [MefFact(CompositionEngines.V2)]
        public void CustomMetadataValueV2(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPart>();
            Assert.IsType<MyObjectType>(importer.ImportingProperty.Metadata["SomeName"]);
        }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithCustomMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export]
        [MefV1.Export]
        [CustomMetadata]
        public class ExportWithCustomMetadata { }

        [MetadataAttribute]
        [MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class CustomMetadataAttribute : Attribute
        {
            public CustomMetadataAttribute()
            {
                this.SomeName = new MyObjectType(5);
            }

            public MyObjectType SomeName { get; set; }
        }

        public class MyObjectType
        {
            internal MyObjectType(int value)
            {
            }
        }
    }
}
