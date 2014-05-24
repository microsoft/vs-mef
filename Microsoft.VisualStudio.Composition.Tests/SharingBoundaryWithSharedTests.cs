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
    using MefV1 = System.ComponentModel.Composition;

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

        [MefFact(CompositionEngines.V2Compat)]
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

        [MefFact(CompositionEngines.V3EmulatingV1AndV2AtOnce)]
        public void ScopedSharedPartsIsolatedToSharingBoundaryPartWithV1AndV2(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();

            var subscope1 = root.Factory.CreateExport().Value;
            var subscope2 = root.Factory.CreateExport().Value;

            foreach (var export in subscope1.BoundaryScopedSharedParts)
            {
                // If this fails, it means that scoped parts are being inappropriately shared between
                // instances of the sub-scopes.
                Assert.False(subscope2.BoundaryScopedSharedParts.Contains(export), export.GetType().Name + " is improperly shared across instances of a sharing boundary.");
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

        [MefFact(CompositionEngines.V2)]
        public void DisposeExportDisposesContainer(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var boundaryExport = root.Factory.CreateExport();
            var subcontainerPart = boundaryExport.Value.BoundaryScopedSharedParts.OfType<SharedPartThatImportsBoundaryPart>().Single();
            Assert.Equal(0, boundaryExport.Value.DisposalCount);
            Assert.Equal(0, subcontainerPart.DisposalCount);

            boundaryExport.Dispose();
            Assert.Equal(1, boundaryExport.Value.DisposalCount);
            Assert.Equal(1, subcontainerPart.DisposalCount);
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
        public class BoundaryPart : IDisposable
        {
            internal int DisposalCount { get; private set; }

            [ImportMany("SharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedSharedParts { get; set; }

            [Import]
            public PartWithImportManyOfScopedExports ImportManyPart1 { get; set; }

            [Import]
            public AnotherPartWithImportManyOfScopedExports ImportManyPart2 { get; set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }
        }

        [Export, Export("SharedWithinBoundaryParts", typeof(object))]
        [Shared("SomeBoundary")]
        public class SharedPartThatImportsBoundaryPart : IDisposable
        {
            internal int DisposalCount { get; private set; }

            [Import]
            public BoundaryPart BoundaryPart { get; set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }
        }

        [Export, Export("SharedWithinBoundaryParts", typeof(object))]
        [Shared("SomeBoundary")]
        public class SharedPartThatOptionallyImportsBoundaryPart
        {
            [Import(AllowDefault = true)]
            public BoundaryPart BoundaryPart { get; set; }
        }

        [Export, Export("SharedWithinBoundaryParts", typeof(object))]
        [Shared("SomeBoundary")]
        public class SharedPartThatIndirectlyImportsBoundaryPart
        {
            [Import]
            public SharedPartThatImportsBoundaryPart BoundaryImportingPart { get; set; }
        }

        [MefV1.Export, MefV1.Export("SharedWithinBoundaryParts", typeof(object))]
        public class V1SharedPartThatImportsBoundaryPart : IDisposable
        {
            internal int DisposalCount { get; private set; }

            [MefV1.Import]
            public BoundaryPart BoundaryPart { get; set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }
        }

        [MefV1.Export, MefV1.Export("SharedWithinBoundaryParts", typeof(object))]
        public class V1SharedPartThatOptionallyImportsBoundaryPart
        {
            [MefV1.Import(AllowDefault = true)]
            public BoundaryPart BoundaryPart { get; set; }
        }

        [MefV1.Export, MefV1.Export("SharedWithinBoundaryParts", typeof(object))]
        public class V1SharedPartThatIndirectlyImportsBoundaryPart
        {
            [MefV1.Import]
            public SharedPartThatImportsBoundaryPart BoundaryImportingPart { get; set; }
        }
    }
}
