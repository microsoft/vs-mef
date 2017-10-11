// Copyright (c) Microsoft. All rights reserved.

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

        [Trait("SharingBoundary", "Isolation")]
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

        [Trait("Disposal", "")]
        [MefFact(CompositionEngines.V2Compat)]
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

        [MefFact(CompositionEngines.V2Compat)]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void DisposeExportReleasesContainer(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var boundaryExport = root.Factory.CreateExport();
            var subcontainerPart = boundaryExport.Value.BoundaryScopedSharedParts.OfType<SharedPartThatImportsBoundaryPart>().Single();

            WeakReference boundaryExportWeak = new WeakReference(boundaryExport.Value);
            WeakReference subcontainerPartWeak = new WeakReference(subcontainerPart);
            WeakReference sharedSubScopePart = new WeakReference(boundaryExport.Value.BoundaryScopedSharedParts[0]);

            boundaryExport.Dispose();
            boundaryExport = null;
            subcontainerPart = null;

            GC.Collect();

            Assert.False(boundaryExportWeak.IsAlive);
            Assert.False(subcontainerPartWeak.IsAlive);
            Assert.False(sharedSubScopePart.IsAlive);
        }

        [Export]
        public class RootPart
        {
            [Import, SharingBoundary("SomeBoundary")]
            public ExportFactory<BoundaryPart> Factory { get; set; }
        }

        /// <summary>
        /// This part is part of a test for root parts that are constructed from a child scope.
        /// It should not be imported by any part in the root scope or it will defeat the test.
        /// </summary>
        [Export, Shared]
        public class SharedRootPartOnlyImportedFromChildScope
        {
            public SharedRootPartOnlyImportedFromChildScope()
            {
                Assert.False(true, "This type should ever be constructed. It should be imported lazily and never constructed as part of a GC test.");
            }
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

            /// <summary>
            /// Gets or sets a lazy import that is meant to help with GC tests.
            /// It must never be constructed, lest the value factory be released and defeat the test.
            /// </summary>
            [Import]
            public Lazy<SharedRootPartOnlyImportedFromChildScope> SharedRootPart { get; set; }

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
