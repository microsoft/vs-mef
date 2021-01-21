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

    [Trait("SharingBoundary", "")]
    public class MultipleSharingBoundariesTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void DualBoundaryPartAccessibleViaOneBoundaryAtATime(IContainer container)
        {
            BoundaryFactory root = container.GetExportedValue<BoundaryFactory>();
            Boundary1Part boundary1Part = root.Boundary1Factory.CreateExport().Value;
            Boundary2Part boundary2Part = boundary1Part.Boundary2Factory.CreateExport().Value;
            PartImportingFromBothBoundaries? dualBoundaryPart = boundary2Part.DualBoundaryPart;
            Assert.NotNull(dualBoundaryPart);
            Assert.Same(boundary2Part, dualBoundaryPart!.Boundary2Value);
            Assert.Same(boundary1Part, dualBoundaryPart.Boundary1Value);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void DualBoundaryPartAccessibleViaTwoBoundariesAtOnce(IContainer container)
        {
            BoundaryFactory root = container.GetExportedValue<BoundaryFactory>();
            PartImportingFromBothBoundaries dualBoundaryPart = root.DualBoundaryPartFactory.CreateExport().Value;
            Assert.NotNull(dualBoundaryPart);
            Assert.NotNull(dualBoundaryPart.Boundary1Value);
            Assert.NotNull(dualBoundaryPart.Boundary2Value);
        }

        [Export]
        public class BoundaryFactory
        {
            [Import, SharingBoundary("Boundary1")]
            public ExportFactory<Boundary1Part> Boundary1Factory { get; set; } = null!;

            [Import, SharingBoundary("Boundary1", "Boundary2")]
            public ExportFactory<PartImportingFromBothBoundaries> DualBoundaryPartFactory { get; set; } = null!;
        }

        [Export, Shared("Boundary1")]
        public class Boundary1Part
        {
            [Import, SharingBoundary("Boundary2")]
            public ExportFactory<Boundary2Part> Boundary2Factory { get; set; } = null!;
        }

        [Export, Shared("Boundary2")]
        public class Boundary2Part
        {
            [Import(AllowDefault = true)]
            public PartImportingFromBothBoundaries? DualBoundaryPart { get; set; }
        }

        [Export]
        public class PartImportingFromBothBoundaries
        {
            [Import]
            public Boundary1Part Boundary1Value { get; set; } = null!;

            [Import]
            public Boundary2Part Boundary2Value { get; set; } = null!;
        }
    }
}
