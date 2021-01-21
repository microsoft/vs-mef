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
    using MefV1 = System.ComponentModel.Composition;

    public class LazyImportTests
    {
        public LazyImportTests()
        {
            AnotherExport.ConstructionCount = 0;
        }

        #region LazyImport test

        [MefFact(CompositionEngines.V2Compat, typeof(ExportWithLazyImport), typeof(AnotherExport))]
        public void LazyImport(IContainer container)
        {
            var lazyImport = container.GetExportedValue<ExportWithLazyImport>();
            Assert.Equal(0, AnotherExport.ConstructionCount);
            Assert.False(lazyImport.AnotherExport.IsValueCreated);
            AnotherExport anotherExport = lazyImport.AnotherExport.Value;
            Assert.Equal(1, AnotherExport.ConstructionCount);

            // Verify that another instance gets its own instance of what it's importing (since it's non-shared).
            var lazyImport2 = container.GetExportedValue<ExportWithLazyImport>();
            Assert.Equal(1, AnotherExport.ConstructionCount);
            Assert.False(lazyImport2.AnotherExport.IsValueCreated);
            AnotherExport anotherExport2 = lazyImport2.AnotherExport.Value;
            Assert.Equal(2, AnotherExport.ConstructionCount);
            Assert.NotSame(anotherExport, anotherExport2);
        }

        [Export]
        public class ExportWithLazyImport
        {
            [Import]
            public Lazy<AnotherExport> AnotherExport { get; set; } = null!;
        }

        #endregion

        #region LazyImportByBaseType test

        [MefFact(CompositionEngines.V2Compat, typeof(ExportWithLazyImportOfBaseType), typeof(AnotherExport))]
        public void LazyImportByBaseType(IContainer container)
        {
            var lazyImport = container.GetExportedValue<ExportWithLazyImportOfBaseType>();
            Assert.IsType(typeof(AnotherExport), lazyImport.AnotherExport.Value);
        }

        [Export]
        public class ExportWithLazyImportOfBaseType
        {
            [Import("AnotherExport")]
            public Lazy<object> AnotherExport { get; set; } = null!;
        }

        #endregion

        #region LazyImportMany test

        [MefFact(CompositionEngines.V2Compat, typeof(ExportWithListOfLazyImport), typeof(AnotherExport))]
        public void LazyImportMany(IContainer container)
        {
            var lazyImport = container.GetExportedValue<ExportWithListOfLazyImport>();
            Assert.Equal(1, lazyImport.AnotherExports.Count);
            Assert.Equal(0, AnotherExport.ConstructionCount);
            Assert.False(lazyImport.AnotherExports[0].IsValueCreated);
            AnotherExport anotherExport = lazyImport.AnotherExports[0].Value;
            Assert.Equal(1, AnotherExport.ConstructionCount);
        }

        [Export]
        public class ExportWithListOfLazyImport
        {
            [ImportMany]
            public IList<Lazy<AnotherExport>> AnotherExports { get; set; } = null!;
        }

        #endregion

        #region Lazy entrypoint to a non-lazy chain of imports

        /// <summary>
        /// Verifies that imports are satisfied deeply.
        /// </summary>
        /// <remarks>
        /// This may seem arbitrary but when this test was written, there was a bug that only manifested
        /// after the sixth link. I added several more for durability of the test.
        /// </remarks>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Link1), typeof(Link2), typeof(Link3), typeof(Link4), typeof(Link5), typeof(Link6), typeof(Link7), typeof(Link8), typeof(Link9), typeof(AnotherExport))]
        public void LazyEntrypointToNonLazyChain(IContainer container)
        {
            Link2.CtorInvocationCounter = 0;
            var chain = container.GetExportedValue<Link1>();
            Assert.NotNull(chain.Link);
            Assert.Equal(0, Link2.CtorInvocationCounter);
            Assert.NotNull(chain.Link.Value);
            Assert.Equal(1, Link2.CtorInvocationCounter);
            Assert.NotNull(chain.Link.Value.Link);
            Assert.NotNull(chain.Link.Value.Link.Link);
            Assert.NotNull(chain.Link.Value.Link.Link.Link);
            Assert.NotNull(chain.Link.Value.Link.Link.Link.Link);
            Assert.NotNull(chain.Link.Value.Link.Link.Link.Link.Link);
            Assert.NotNull(chain.Link.Value.Link.Link.Link.Link.Link.Link);
            Assert.NotNull(chain.Link.Value.Link.Link.Link.Link.Link.Link.Link);
            Assert.NotNull(chain.Link.Value.Link.Link.Link.Link.Link.Link.Link.Link);
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link1
        {
            [Import, MefV1.Import]
            public Lazy<Link2> Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link2
        {
            internal static int CtorInvocationCounter;

            public Link2()
            {
                CtorInvocationCounter++;
            }

            [Import, MefV1.Import]
            public Link3 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link3
        {
            [Import, MefV1.Import]
            public Link4 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link4
        {
            [Import, MefV1.Import]
            public Link5 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link5
        {
            [Import, MefV1.Import]
            public Link6 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link6
        {
            [Import, MefV1.Import]
            public Link7 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link7
        {
            [Import, MefV1.Import]
            public Link8 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link8
        {
            [Import, MefV1.Import]
            public Link9 Link { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class Link9
        {
            [Import, MefV1.Import]
            public AnotherExport Link { get; set; } = null!;
        }

        #endregion

        [Export]
        [Export("AnotherExport", typeof(object))]
        [MefV1.Export]
        public class AnotherExport
        {
            internal static int ConstructionCount;

            public AnotherExport()
            {
                ConstructionCount++;
            }
        }
    }
}
