namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;

    [Trait("SharingBoundary", "")]
    public class SharingBoundaryInvalidTests
    {
        /// <summary>
        /// This test documents that V2 considers this an invalid graph.
        /// </summary>
        [MefFact(CompositionEngines.V2, NoCompatGoal = true, InvalidConfiguration = true)]
        public void InvalidImportAcrossSharingBoundaryV2(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var boundaryPartExport = root.Factory.CreateExport();
        }

        /// <summary>
        /// This test documents that V3 works where V2 doesn't,
        /// because it automatically propagates sharing boundaries across imports.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV2)]
        public void InvalidImportAcrossSharingBoundaryV3(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var boundaryPartExport = root.Factory.CreateExport();
        }

        [Export]
        public class RootPart
        {
            [Import, SharingBoundary("SomeBoundary")]
            public ExportFactory<BoundaryPart> Factory { get; set; }
        }

        [Export, Shared("SomeBoundary")]
        public class BoundaryPart
        {
            [Import]
            public PartThatImportsBoundaryPartFromOutsideBoundary BoundaryScopedSharedParts { get; set; }
        }

        [Export, Shared]
        public class PartThatImportsBoundaryPartFromOutsideBoundary
        {
            [Import]
            public BoundaryPart BoundaryPart { get; set; }
        }
    }
}
