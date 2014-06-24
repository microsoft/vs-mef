namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
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

            var importDefinition = new ImportDefinition(
                typeof(SomeOtherPart).FullName,
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary<string, object>.Empty,
                ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty);
            IEnumerable<Export> exports = exportProvider.GetExports(importDefinition);
            var otherPart2 = exports.Single().Value;
            Assert.NotNull(otherPart2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
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

        [MefFact(CompositionEngines.V1/*Compat*/, typeof(Apple))]
        public void GetExportOfTypeByObjectAndContractName(IContainer container)
        {
            var apple = container.GetExportedValue<object>("SomeContract");
            Assert.IsType(typeof(Apple), apple);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Apple))]
        public void GetExportOfTypeByBaseTypeAndContractName(IContainer container)
        {
            var apples = container.GetExportedValues<Fruit>("SomeContract");
            Assert.Equal(0, apples.Count());
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

        public class Fruit { }

        [Export("SomeContract")]
        [MefV1.Export("SomeContract")]
        public class Apple : Fruit { }
    }
}
