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
    using MefV1 = System.ComponentModel.Composition;

    [Trait("SharingBoundary", "")]
    public class SharingBoundaryMixedAttributesTests
    {
        [MefFact(CompositionEngines.V3EmulatingV1AndV2AtOnce)]
        [Trait("SharingBoundary", "Isolation")]
        public void DisposeExportReleasesContainer(IContainer container)
        {
            var rootPart = container.GetExportedValue<RootPart>();
            var boundaryPartExport = rootPart.Factory.CreateExport();
            var factoryPart = boundaryPartExport.Value.NonSharedPartFactory;

            var boundaryPartWeak = new WeakReference(boundaryPartExport.Value);
            var factoryPartWeak = new WeakReference(factoryPart);

            boundaryPartExport.Dispose();
            boundaryPartExport = null;
            factoryPart = null;

            GC.Collect();

            Assert.False(boundaryPartWeak.IsAlive);
            Assert.False(factoryPartWeak.IsAlive);
        }

        [MefFact(CompositionEngines.V3EmulatingV1AndV2AtOnce)]
        [Trait("SharingBoundary", "Isolation")]
        public void ScopedNonSharedPartsIsolatedToExportFactorySharingBoundaryPart(IContainer container)
        {
            var rootPart = container.GetExportedValue<RootPart>();

            var boundary1 = rootPart.Factory.CreateExport();
            var boundary2 = rootPart.Factory.CreateExport();
            Assert.NotSame(boundary1, boundary2);

            var factoryPart1 = boundary1.Value.NonSharedPartFactory;
            var factoryPart2 = boundary2.Value.NonSharedPartFactory;
            Assert.NotSame(factoryPart1, factoryPart2);

            var boundary1CreatedPart = factoryPart1.Factory.CreateExport();
            Assert.Same(boundary1.Value, boundary1CreatedPart.Value.BoundaryPart);

            var boundary2CreatedPart = factoryPart2.Factory.CreateExport();
            Assert.Same(boundary2.Value, boundary2CreatedPart.Value.BoundaryPart);
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
            public BoundaryPart() { }

            [Import]
            public PartWithFactoryOfPartThatImportsSharingBoundary NonSharedPartFactory { get; set; }
        }

        [MefV1.Export]
        public class PartWithFactoryOfPartThatImportsSharingBoundary
        {
            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPartThatImportsBoundaryPart> Factory { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPartThatImportsBoundaryPart
        {
            [MefV1.Import]
            public BoundaryPart BoundaryPart { get; set; }
        }
    }
}
