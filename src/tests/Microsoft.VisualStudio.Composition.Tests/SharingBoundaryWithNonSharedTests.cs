// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public class SharingBoundaryWithNonSharedTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void NonSharedPartImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<NonSharedPartThatImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void BoundaryPartNotAvailableFromRoot(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<BoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void NonSharedPartOptionallyImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<NonSharedPartThatOptionallyImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void NonSharedPartIndirectlyImportsPartFromSharingBoundary(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<NonSharedPartThatIndirectlyImportsBoundaryPart>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ScopedNonSharedPartsAvailableToSharingBoundaryPart(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var subscope = root.Factory.CreateExport().Value;
            Assert.Equal(3, subscope.BoundaryScopedNonSharedParts.Count);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ScopedNonSharedPartsIsolatedToSharingBoundaryPart(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();

            var subscope1 = root.Factory.CreateExport().Value;
            var subscope2 = root.Factory.CreateExport().Value;

            foreach (var export in subscope1.BoundaryScopedNonSharedParts)
            {
                // If this fails, it means that scoped parts are being inappropriately shared between
                // instances of the sub-scopes.
                Assert.False(subscope2.BoundaryScopedNonSharedParts.Contains(export));
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
            public ExportFactory<BoundaryPart> Factory { get; set; } = null!;
        }

        [Export]
        public class PartWithImportManyOfScopedExports
        {
            [ImportMany("NonSharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedNonSharedParts { get; set; } = null!;
        }

        [Export, Shared("SomeBoundary")]
        public class BoundaryPart
        {
            [ImportMany("NonSharedWithinBoundaryParts")]
            public IList<object> BoundaryScopedNonSharedParts { get; set; } = null!;

            [Import]
            public PartWithImportManyOfScopedExports ImportManyPart { get; set; } = null!;
        }

        [Export, Export("NonSharedWithinBoundaryParts", typeof(object))]
        public class NonSharedPartThatImportsBoundaryPart
        {
            [Import]
            public BoundaryPart BoundaryPart { get; set; } = null!;
        }

        [Export, Export("NonSharedWithinBoundaryParts", typeof(object))]
        public class NonSharedPartThatOptionallyImportsBoundaryPart
        {
            [Import(AllowDefault = true)]
            public BoundaryPart BoundaryPart { get; set; } = null!;
        }

        [Export, Export("NonSharedWithinBoundaryParts", typeof(object))]
        public class NonSharedPartThatIndirectlyImportsBoundaryPart
        {
            [Import]
            public NonSharedPartThatImportsBoundaryPart BoundaryImportingPart { get; set; } = null!;
        }
    }
}
