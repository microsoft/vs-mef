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

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsExportProvider
        {
            [Import, MefV1.Import]
            public ExportProvider ExportProvider { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class SomeOtherPart { }
    }
}
