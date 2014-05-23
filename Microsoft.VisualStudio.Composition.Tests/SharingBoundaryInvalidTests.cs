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

    public class SharingBoundaryInvalidTests
    {
        [MefFact(CompositionEngines.V2Compat, InvalidConfiguration = true, Skip = "Not yet passing")]
        public void InvalidImportAcrossSharingBoundary(IContainer container)
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
            internal int DisposalCount { get; private set; }

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
