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

    [Trait("SharingBoundary", "")]
    public class SharingBoundaryWithSharedTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void SharedPartImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<SharedPartThatImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void BoundaryPartNotAvailableFromRoot(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<BoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void SharedPartOptionallyImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<SharedPartThatOptionallyImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void SharedPartIndirectlyImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<SharedPartThatIndirectlyImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ScopedSharedPartsAvailableToSharingBoundaryPart(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var subscope = root.Factory.CreateExport().Value;
            Assert.Equal(3, subscope.BoundaryScopedSharedParts.Count);
        }

        [MefFact(CompositionEngines.V2)]
        public void ScopedSharedPartsIsolatedToSharingBoundaryPart(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();

            var subscope1 = root.Factory.CreateExport().Value;
            var subscope2 = root.Factory.CreateExport().Value;

            foreach (var export in subscope1.BoundaryScopedSharedParts)
            {
                // If this fails, it means that scoped parts are being inappropriately shared between
                // instances of the sub-scopes.
                Assert.False(subscope2.BoundaryScopedSharedParts.Contains(export));
            }
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ScopedSharedPartsSharedWithinBoundary(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();

            var subscope = root.Factory.CreateExport().Value;

            Assert.Equal(3, subscope.ImportManyPart1.BoundaryScopedSharedParts.Count);
            Assert.Equal(3, subscope.ImportManyPart2.BoundaryScopedSharedParts.Count);

            foreach (var item in subscope.ImportManyPart1.BoundaryScopedSharedParts)
            {
                // The importmany collection should be populated with shared exports within the sharing boundary.
                Assert.True(subscope.ImportManyPart2.BoundaryScopedSharedParts.Contains(item));
            }
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportManyPullsPartIntoSharedBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PartWithImportManyOfScopedExports>());
        }

        [Export]
        public class RootPart
        {
            [Import, SharingBoundary("SomeBoundary")]
            public ExportFactory<BoundaryPart> Factory { get; set; }
        }

        [Export]
        public class PartWithImportManyOfScopedExports
        {
            [ImportMany("SharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedSharedParts { get; set; }
        }

        [Export]
        public class AnotherPartWithImportManyOfScopedExports
        {
            [ImportMany("SharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedSharedParts { get; set; }
        }

        [Export, Shared("SomeBoundary")]
        public class BoundaryPart
        {
            [ImportMany("SharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedSharedParts { get; set; }

            [Import]
            public PartWithImportManyOfScopedExports ImportManyPart1 { get; set; }

            [Import]
            public AnotherPartWithImportManyOfScopedExports ImportManyPart2 { get; set; }
        }

        [Export, Export("SharedWithinBoundaryParts", typeof(object))]
        [Shared("SomeBoundary")] // TODO: try removing the argument from this attribute
        public class SharedPartThatImportsBoundaryPart
        {
            [Import]
            public BoundaryPart BoundaryPart { get; set; }
        }

        [Export, Export("SharedWithinBoundaryParts", typeof(object))]
        [Shared("SomeBoundary")] // TODO: try removing the argument from this attribute
        public class SharedPartThatOptionallyImportsBoundaryPart
        {
            [Import(AllowDefault = true)]
            public BoundaryPart BoundaryPart { get; set; }
        }

        [Export, Export("SharedWithinBoundaryParts", typeof(object))]
        [Shared("SomeBoundary")] // TODO: try removing the argument from this attribute
        public class SharedPartThatIndirectlyImportsBoundaryPart
        {
            [Import]
            public SharedPartThatImportsBoundaryPart BoundaryImportingPart { get; set; }
        }
    }
}
