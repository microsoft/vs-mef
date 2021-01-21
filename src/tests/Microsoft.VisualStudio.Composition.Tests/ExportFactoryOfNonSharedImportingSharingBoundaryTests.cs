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
    [Trait("ExportFactory", "")]
    public class ExportFactoryOfNonSharedImportingSharingBoundaryTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void ExportFactoryOfNonSharedImportingSharingBoundary(IContainer container)
        {
            var factoryPart = container.GetExportedValue<RootPart>();
            var nonSharedPart1 = factoryPart.Factory.CreateExport().Value;
            var nonSharedPart2 = factoryPart.Factory.CreateExport().Value;
            Assert.NotSame(nonSharedPart1.SharingBoundaryImport, nonSharedPart2.SharingBoundaryImport);
        }

        [Export, Shared("SharingBoundary")]
        public class SharingBoundaryPart { }

        [Export]
        public class NonSharedBoundaryImportingPart
        {
            [Import]
            public SharingBoundaryPart SharingBoundaryImport { get; set; } = null!;
        }

        [Export]
        public class RootPart
        {
            [Import, SharingBoundary("SharingBoundary")]
            public ExportFactory<NonSharedBoundaryImportingPart> Factory { get; set; } = null!;
        }
    }
}
