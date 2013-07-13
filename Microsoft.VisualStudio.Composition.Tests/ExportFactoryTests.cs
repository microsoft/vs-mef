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

        [MefFact(CompositionEngines.V1)]
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

        [MefFact(CompositionEngines.V2)]
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

        [MefV1.Export]
        public class PartFactoryV1
        {
            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart> Factory { get; set; }

            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart, IDictionary<string, object>> FactoryWithMetadata { get; set; }
        }

        [Export]
        public class PartFactoryV2
        {
            [Import]
            public ExportFactory<NonSharedPart> Factory { get; set; }

            [Import]
            public ExportFactory<NonSharedPart, IDictionary<string, object>> FactoryWithMetadata { get; set; }
        }

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
    }
}
