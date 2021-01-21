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

    public class SharedNoBoundaryExportsTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void AcquireSharedExportTwiceYieldsSameInstance(IContainer container)
        {
            var firstResult = container.GetExportedValue<SharedExport>();
            var secondResult = container.GetExportedValue<SharedExport>();
            Assert.NotNull(firstResult);
            Assert.NotNull(secondResult);
            Assert.Same(firstResult, secondResult);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ImportingSharedExportAtMultipleSitesYieldsSameInstance(IContainer container)
        {
            var importer1 = container.GetExportedValue<Importer1>();
            var importer2 = container.GetExportedValue<Importer2>();
            Assert.NotNull(importer1.ImportingProperty1);
            Assert.NotNull(importer1.ImportingProperty2);
            Assert.NotNull(importer2.ImportingProperty1);
            Assert.NotNull(importer2.ImportingProperty2);
            Assert.Same(importer1.ImportingProperty1, importer1.ImportingProperty2);
            Assert.Same(importer2.ImportingProperty1, importer2.ImportingProperty2);
            Assert.Same(importer1.ImportingProperty1, importer2.ImportingProperty1);
        }

        [Export, Shared]
        public class SharedExport { }

        [Export]

        public class Importer1
        {
            [Import]
            public SharedExport ImportingProperty1 { get; set; } = null!;

            [Import]
            public SharedExport ImportingProperty2 { get; set; } = null!;
        }

        [Export]
        public class Importer2
        {
            [Import]
            public SharedExport ImportingProperty1 { get; set; } = null!;

            [Import]
            public SharedExport ImportingProperty2 { get; set; } = null!;
        }
    }
}
