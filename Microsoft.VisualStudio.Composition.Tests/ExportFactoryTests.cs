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

    public class ExportFactoryTests
    {
        public ExportFactoryTests()
        {
            NonSharedPart.InstantiationCounter = 0;
            NonSharedPart.DisposalCounter = 0;
        }

        #region V1 tests

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(NonSharedPart))]
        public void ExportFactoryForNonSharedPartV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            Assert.NotNull(partFactory.Factory);
            Assert.NotNull(partFactory.FactoryWithMetadata);
            Assert.Equal("V", partFactory.FactoryWithMetadata.Metadata["N"]);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(1, NonSharedPart.DisposalCounter);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryManyV1), typeof(NonSharedPart), typeof(NonSharedPart2))]
        public void ExportFactoryForNonSharedPartManyV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryManyV1>();
            Assert.NotNull(partFactory.Factories);
            Assert.Equal(2, partFactory.Factories.Count());

            Assert.NotNull(partFactory.FactoriesWithMetadata);
            Assert.Equal(2, partFactory.FactoriesWithMetadata.Count());
            var factory1 = partFactory.FactoriesWithMetadata.Single(f => "V".Equals(f.Metadata["N"]));
            var factory2 = partFactory.FactoriesWithMetadata.Single(f => "V2".Equals(f.Metadata["N"]));

            using (var exportContext = factory1.CreateExport())
            {
                Assert.IsType<NonSharedPart>(exportContext.Value);
            }

            using (var exportContext = factory2.CreateExport())
            {
                Assert.IsType<NonSharedPart2>(exportContext.Value);
            }
        }

        [MefV1.Export]
        public class PartFactoryV1
        {
            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart> Factory { get; set; }

            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart, IDictionary<string, object>> FactoryWithMetadata { get; set; }
        }

        [MefV1.Export]
        public class PartFactoryManyV1
        {
            [MefV1.ImportMany]
            public IEnumerable<MefV1.ExportFactory<NonSharedPart>> Factories { get; set; }

            [MefV1.ImportMany]
            public IEnumerable<MefV1.ExportFactory<NonSharedPart, IDictionary<string, object>>> FactoriesWithMetadata { get; set; }
        }

        #endregion

        #region V2 tests

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(NonSharedPart))]
        public void ExportFactoryForNonSharedPartV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            Assert.NotNull(partFactory.Factory);
            Assert.NotNull(partFactory.FactoryWithMetadata);
            Assert.Equal("V", partFactory.FactoryWithMetadata.Metadata["N"]);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(1, NonSharedPart.DisposalCounter);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryManyV2), typeof(NonSharedPart), typeof(NonSharedPart2))]
        public void ExportFactoryForNonSharedPartManyV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryManyV2>();
            Assert.NotNull(partFactory.Factories);
            Assert.Equal(2, partFactory.Factories.Count());

            Assert.NotNull(partFactory.FactoriesWithMetadata);
            Assert.Equal(2, partFactory.FactoriesWithMetadata.Count());
            var factory1 = partFactory.FactoriesWithMetadata.Single(f => "V".Equals(f.Metadata["N"]));
            var factory2 = partFactory.FactoriesWithMetadata.Single(f => "V2".Equals(f.Metadata["N"]));

            using (var exportContext = factory1.CreateExport())
            {
                Assert.IsType<NonSharedPart>(exportContext.Value);
            }

            using (var exportContext = factory2.CreateExport())
            {
                Assert.IsType<NonSharedPart2>(exportContext.Value);
            }
        }

        [Export]
        public class PartFactoryV2
        {
            [Import]
            public ExportFactory<NonSharedPart> Factory { get; set; }

            [Import]
            public ExportFactory<NonSharedPart, IDictionary<string, object>> FactoryWithMetadata { get; set; }
        }

        [Export]
        public class PartFactoryManyV2
        {
            [ImportMany]
            public IEnumerable<ExportFactory<NonSharedPart>> Factories { get; set; }

            [ImportMany]
            public IEnumerable<ExportFactory<NonSharedPart, IDictionary<string, object>>> FactoriesWithMetadata { get; set; }
        }

        #endregion

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("N", "V")]
        [Export]
        [ExportMetadata("N", "V")]
        public class NonSharedPart : IDisposable
        {
            internal static int InstantiationCounter;
            internal static int DisposalCounter;

            public NonSharedPart()
            {
                InstantiationCounter++;
            }

            public void Dispose()
            {
                DisposalCounter++;
            }
        }

        [MefV1.Export(typeof(NonSharedPart)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("N", "V2")]
        [Export(typeof(NonSharedPart))]
        [ExportMetadata("N", "V2")]
        public class NonSharedPart2 : NonSharedPart
        {
        }
    }
}
