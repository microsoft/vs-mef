// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    [Trait("SharingBoundary", "")]
    public class SharingBoundaryTwoPathsToPartTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void SharingBoundaryTwoPathsToPart(IContainer container)
        {
            var root = container.GetExportedValue<Root>();

            var a = root.FactoryA.CreateExport().Value;
            var ab1 = a.FactoryB.CreateExport().Value;
            var ab2 = a.FactoryB.CreateExport().Value;
            Assert.Same(ab1.A, ab2.A);
            Assert.NotSame(ab1.B, ab2.B);

            var b = root.FactoryB.CreateExport().Value;
            var ba1 = b.FactoryA.CreateExport().Value;
            var ba2 = b.FactoryA.CreateExport().Value;
            Assert.Same(ba1.B, ba2.B);
            Assert.NotSame(ba1.A, ba2.A);

            Assert.NotSame(ab1, ba1);
            Assert.NotSame(ab1.A, ba1.A);
            Assert.NotSame(ab1.B, ba1.B);
        }

        [Export, Shared("A")]
        public class SharedA
        {
            [Import, SharingBoundary("B")]
            public ExportFactory<NonSharedNeedsBothBoundaries> FactoryB { get; set; } = null!;
        }

        [Export, Shared("B")]
        public class SharedB
        {
            [Import, SharingBoundary("A")]
            public ExportFactory<NonSharedNeedsBothBoundaries> FactoryA { get; set; } = null!;
        }

        [Export]
        public class NonSharedNeedsBothBoundaries
        {
            [Import]
            public SharedA A { get; set; } = null!;

            [Import]
            public SharedB B { get; set; } = null!;
        }

        [Export]
        public class Root
        {
            [Import, SharingBoundary("A")]
            public ExportFactory<SharedA> FactoryA { get; set; } = null!;

            [Import, SharingBoundary("B")]
            public ExportFactory<SharedB> FactoryB { get; set; } = null!;
        }
    }
}
