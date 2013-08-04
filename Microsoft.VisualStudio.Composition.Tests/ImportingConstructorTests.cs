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
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SimpleImportingConstructorPart), typeof(RandomExport))]
        public void SimpleImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<SimpleImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(LazyImportingConstructorPart), typeof(RandomExport))]
        public void LazyImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<LazyImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SpecialImportingConstructorPart), typeof(RandomExportWithContractName), typeof(RandomExport))]
        public void SpecialImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<SpecialImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(PrivateDefaultConstructorPart))]
        public void PrivateDefaultConstructor(IContainer container)
        {
            var part = container.GetExportedValue<PrivateDefaultConstructorPart>();
            Assert.NotNull(part);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(PrivateImportingConstructorPart), typeof(RandomExport))]
        public void PrivateImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<PrivateImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(PrivateImportingConstructorOpenGenericPart<,>), typeof(RandomExport))]
        public void PrivateImportingConstructorOpenGeneric(IContainer container)
        {
            var part = container.GetExportedValue<PrivateImportingConstructorOpenGenericPart<int, string>>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, typeof(ImportingConstructorWithImportManyPart), typeof(RandomExport))]
        public void ImportingConstructorWithImportMany(IContainer container)
        {
            var part = container.GetExportedValue<ImportingConstructorWithImportManyPart>();
            Assert.Equal(1, part.ConstructorImports.Length);
            Assert.IsType<RandomExport>(part.ConstructorImports[0]);
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PrivateDefaultConstructorPart
        {
            private PrivateDefaultConstructorPart()
            {
            }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PrivateImportingConstructorPart
        {
            [MefV1.ImportingConstructor]
            private PrivateImportingConstructorPart(RandomExport export)
            {
                Assert.NotNull(export);
            }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PrivateImportingConstructorOpenGenericPart<T1, T2>
        {
            [MefV1.ImportingConstructor]
            private PrivateImportingConstructorOpenGenericPart(RandomExport export)
            {
                Assert.NotNull(export);
            }
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
        public class LazyImportingConstructorPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public LazyImportingConstructorPart(Lazy<RandomExport> export)
            {
                Assert.NotNull(export);
                Assert.NotNull(export.Value);
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
        public class ImportingConstructorWithImportManyPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public ImportingConstructorWithImportManyPart([ImportMany, MefV1.ImportMany] RandomExport[] exports)
            {
                Assert.NotNull(exports);
                this.ConstructorImports = exports;
            }

            public RandomExport[] ConstructorImports { get; private set; }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class RandomExport { }

        [Export("Special")]
        [MefV1.Export("Special"), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class RandomExportWithContractName { }
    }
}
