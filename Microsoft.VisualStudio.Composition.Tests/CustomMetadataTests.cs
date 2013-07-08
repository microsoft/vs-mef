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

    public class CustomMetadataTests
    {
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void CustomMetadataOnExportedType(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal("Andrew", part.ImportOfType.Metadata["Name"]);
            Assert.Equal(4, part.ImportOfType.Metadata["Age"]);
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void CustomMetadataOnExportedProperty(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal("Andrew", part.ImportOfProperty.Metadata["Name"]);
            Assert.Equal(4, part.ImportOfProperty.Metadata["Age"]);
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void CustomMetadataDictionaryCaseSensitive(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.False(part.ImportOfType.Metadata.ContainsKey("name"));
        }

        [Export, MefV1.Export]
        public class ImportingPart
        {
            [Import, MefV1.Import]
            public Lazy<ExportedTypeWithMetadata, IDictionary<string, object>> ImportOfType { get; set; }

            [Import, MefV1.Import]
            public Lazy<string, IDictionary<string, object>> ImportOfProperty { get; set; }
        }

        [MefV1.Export, NameAndAgeV1(Name = "Andrew", Age = 4)]
        [Export, NameAndAgeV2(Name = "Andrew", Age = 4)]
        public class ExportedTypeWithMetadata { }

        public class TypeWithExportingMemberAndMetadata
        {
            [MefV1.Export, NameAndAgeV1(Name = "Andrew", Age = 4)]
            [Export, NameAndAgeV2(Name = "Andrew", Age = 4)]
            public string SomeValue
            {
                get { return "Foo"; }
            }
        }
    }
}
