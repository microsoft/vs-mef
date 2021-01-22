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
    using MefV1 = System.ComponentModel.Composition;

    [Trait("SharingBoundary", "")]
    public class SharingBoundaryWithMixedMefParts
    {
        [MefFact(CompositionEngines.V3EmulatingV1AndV2AtOnce)]
        public void SharingBoundariesWithV1AttributedPartsInTheMix(IContainer container)
        {
            var root = container.GetExportedValue<Root>();
            var boundary1 = root.Boundary1Factory.CreateExport();

            var boundary2a = boundary1.Value.Boundary2Factory.CreateExport();
            Assert.NotNull(boundary2a.Value.V1Part);

            var boundary2b = boundary1.Value.Boundary2Factory.CreateExport();
            Assert.NotNull(boundary2b.Value.V1Part);

            Assert.NotSame(boundary2a.Value.V1Part, boundary2b.Value.V1Part);
        }

        [Export]
        public class Root
        {
            [Import, SharingBoundary("Boundary1")]
            public ExportFactory<Boundary1Part> Boundary1Factory { get; set; } = null!;
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
            [Import]
            public Boundary1Part Boundary1Part { get; set; } = null!;

            [Import]
            public V1PartBelongingToBoundary2 V1Part { get; set; } = null!;
        }

        [MefV1.Export]
        public class V1PartBelongingToBoundary2
        {
            [MefV1.Import]
            public Boundary2Part Boundary2Part { get; set; } = null!;
        }
    }
}
