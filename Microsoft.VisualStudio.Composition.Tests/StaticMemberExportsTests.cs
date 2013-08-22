namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Static", "")]
    public class StaticMemberExportsTests
    {
        [MefFact(CompositionEngines.V1, typeof(StaticPartWithStaticExports), typeof(ImportingPart))]
        public void ExportingStaticPartStaticProperty(IContainer container)
        {
            container.GetExportedValue<ImportingPart>();
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithStaticExports), typeof(ImportingPart))]
        public void ExportingStaticProperty(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal(PartWithStaticExports.ExportingMember, part.ImportingMember);
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithStaticExports), typeof(ImportManyWithMetadataPart))]
        public void ImportManyMetadataStaticPropertyExport(IContainer container)
        {
            var part = container.GetExportedValue<ImportManyWithMetadataPart>();
            Assert.Equal(1, part.ImportingMember.Count);
            Assert.Equal("SomeValue", part.ImportingMember[0].Metadata["SomeName"]);
            Assert.Equal(PartWithStaticExports.ExportingMember, part.ImportingMember[0].Value);
        }

        public class PartWithStaticExports
        {
            [MefV1.Export]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string ExportingMember
            {
                get { return "Hello"; }
            }
        }

        public static class StaticPartWithStaticExports
        {
            [MefV1.Export]
            [MefV1.ExportMetadata("SomeName", "SomeValue")]
            public static string ExportingMember
            {
                get { return "Hello"; }
            }
        }

        [MefV1.Export]
        public class ImportingPart
        {
            [MefV1.Import]
            public string ImportingMember { get; set; }
        }

        [MefV1.Export]
        public class ImportManyWithMetadataPart
        {
            [MefV1.ImportMany]
            public List<Lazy<string, IDictionary<string, object>>> ImportingMember { get; set; }
        }
    }
}
