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

    public class SharingBoundaryTests
    {
        [MefFact(CompositionEngines.V2)]
        public void NonSharedPartImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<NonSharedPartThatImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2)]
        public void NonSharedPartOptionallyImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<NonSharedPartThatOptionallyImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2)]
        public void NonSharedPartIndirectlyImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<NonSharedPartThatIndirectlyImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2)]
        public void ScopedNonSharedPartsAvailableToSharingBoundaryPart(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var subscope = root.Factory.CreateExport().Value;
            Assert.Equal(3, subscope.BoundaryScopedNonSharedParts.Count);
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
            [ImportMany("NonSharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedNonSharedParts { get; set; }
        }

        [Export, Export("NonSharedWithinBoundaryParts", typeof(object))]
        public class NonSharedPartThatImportsBoundaryPart
        {
            [Import]
            public BoundaryPart BoundaryPart { get; set; }
        }

        [Export, Export("NonSharedWithinBoundaryParts", typeof(object))]
        public class NonSharedPartThatOptionallyImportsBoundaryPart
        {
            [Import(AllowDefault = true)]
            public BoundaryPart BoundaryPart { get; set; }
        }

        [Export, Export("NonSharedWithinBoundaryParts", typeof(object))]
        public class NonSharedPartThatIndirectlyImportsBoundaryPart
        {
            [Import]
            public NonSharedPartThatImportsBoundaryPart BoundaryImportingPart { get; set; }
        }
    }
}
