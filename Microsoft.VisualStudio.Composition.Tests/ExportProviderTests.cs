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

    public class ExportProviderTests
    {
        [MefFact(CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1, typeof(PartThatImportsExportProvider), typeof(SomeOtherPart))]
        public void GetExportsNonGeneric(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            var exportProvider = importer.ExportProvider;

            IEnumerable<ILazy<object>> exports = exportProvider.GetExports(typeof(SomeOtherPart), null);
            var otherPart2 = exports.Single().Value;
            Assert.NotNull(otherPart2);
        }

        [MefFact(CompositionEngines.V1/*Compat | CompositionEngines.V3EmulatingV2*/, typeof(SomeOtherPart))]
        public void GetExportWithMetadataDictionary(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, IDictionary<string, object>>();
            Assert.Equal(1, export.Metadata["A"]);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1/*Compat | CompositionEngines.V3EmulatingV2*/, typeof(SomeOtherPart))]
        public void GetExportWithMetadataView(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, SomeOtherPartMetadataView>();
            Assert.Equal(1, export.Metadata.A);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1/*Compat | CompositionEngines.V3EmulatingV2*/, typeof(SomeOtherPart))]
        public void GetExportWithFilteringMetadataView(IContainer container)
        {
            var exports = container.GetExports<SomeOtherPart, MetadataViewWithBMember>();
            Assert.Equal(0, exports.Count());
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsExportProvider
        {
            [Import, MefV1.Import]
            public ExportProvider ExportProvider { get; set; }
        }

        [Export, Shared, ExportMetadata("A", 1)]
        [MefV1.Export, MefV1.ExportMetadata("A", 1)]
        public class SomeOtherPart { }

        public interface SomeOtherPartMetadataView
        {
            int A { get; }
        }

        public interface MetadataViewWithBMember
        {
            int B { get; }
        }
    }
}
