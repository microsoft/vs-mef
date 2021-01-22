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
    public class SharingBoundaryAcquiresSharedRootScopeExport
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void SubscopedPartImportsSharedRootPart(IContainer container)
        {
            // Reset counters since this test runs in multiple contexts.
            Root2.InstantiationCounter = 0;
            Root3.InstantiationCounter = 0;

            var root = container.GetExportedValue<Root1>();
            var boundaryPart = root.SubScopeFactory.CreateExport().Value;

            // Be sure that the first explicit reference to Root3 is from the sub-scope.
            // This helps the validity of the point of this test.
            Assert.Equal(0, Root2.InstantiationCounter);
            Assert.Equal(0, Root3.InstantiationCounter);

            // Now the crux of the test: make sure that as Root3 is created in the sub-scope,
            // that the parent scope shares that same instance.
            var root3 = boundaryPart.Root3.Value;
            Assert.Same(root3, root.Root2.Value.Root3.Value);
        }

        [Export, Shared]
        public class Root1
        {
            [Import, SharingBoundary("A")]
            public ExportFactory<SharingBoundaryPart> SubScopeFactory { get; set; } = null!;

            [Import]
            public Lazy<Root2> Root2 { get; set; } = null!;
        }

        [Export, Shared]
        public class Root2
        {
            internal static int InstantiationCounter;

            public Root2()
            {
                InstantiationCounter++;
            }

            [Import]
            public Lazy<Root3> Root3 { get; set; } = null!;
        }

        [Export, Shared]
        public class Root3
        {
            internal static int InstantiationCounter;

            public Root3()
            {
                InstantiationCounter++;
            }
        }

        [Export, Shared("A")]
        public class SharingBoundaryPart
        {
            [Import]
            public Lazy<Root3> Root3 { get; set; } = null!;
        }
    }
}
