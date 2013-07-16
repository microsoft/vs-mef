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

    public class ImportingConstructorTests
    {
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, typeof(SimpleImportingConstructorPart), typeof(RandomExport))]
        public void SimpleImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<SimpleImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, typeof(SpecialImportingConstructorPart), typeof(RandomExportWithContractName), typeof(RandomExport))]
        public void SpecialImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<SpecialImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class SimpleImportingConstructorPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public SimpleImportingConstructorPart(RandomExport export)
            {
                Assert.NotNull(export);
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class SpecialImportingConstructorPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public SpecialImportingConstructorPart([Import("Special"), MefV1.Import("Special")] RandomExportWithContractName specialExport, RandomExport randomExport)
            {
                Assert.NotNull(specialExport);
                Assert.NotNull(randomExport);
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class RandomExport { }

        [Export("Special")]
        [MefV1.Export("Special"), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class RandomExportWithContractName { }
    }
}
