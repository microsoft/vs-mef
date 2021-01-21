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
    public class SharingBoundaryInvalidTests
    {
        /// <summary>
        /// Verifies that an illegal import across sharing boundaries is not allowed.
        /// </summary>
        [MefFact(CompositionEngines.V2Compat)]
        public void InvalidImportAcrossSharingBoundary(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            try
            {
                // PartThatImportsBoundaryPartFromOutsideBoundary's own documented sharing boundary is ""
                // which means it is shared across all other (child) sharing boundaries. Yet it imports
                // a part from a lower sharing boundary which would be an impossible feat to do both.
                // An earlier version of MEFv3 *did* claim to do this, by considering that
                // PartThatImportsBoundaryPartFromOutsideBoundary belonged to multiple sharing boundaries.
                // But that's not really a tenable prospect and it's harder to reason over anyway.
                // It makes sense that MEFv2 didn't allow this.
                root.Factory.CreateExport();
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (CompositionFailedException) { }
            catch (System.Composition.Hosting.CompositionFailedException) { }
        }

        [Export]
        public class RootPart
        {
            [Import, SharingBoundary("SomeBoundary")]
            public ExportFactory<BoundaryPart> Factory { get; set; } = null!;
        }

        [Export, Shared("SomeBoundary")]
        public class BoundaryPart
        {
            [Import]
            public PartThatImportsBoundaryPartFromOutsideBoundary BoundaryScopedSharedParts { get; set; } = null!;
        }

        [Export, Shared]
        public class PartThatImportsBoundaryPartFromOutsideBoundary
        {
            [Import]
            public BoundaryPart BoundaryPart { get; set; } = null!;
        }
    }
}
