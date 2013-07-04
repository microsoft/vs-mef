namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
using Xunit;

    public class LazyImportTests
    {
        [Fact]
        public void LazyImport()
        {
            var container = TestUtilities.CreateContainer(typeof(ExportWithLazyImport), typeof(AnotherExport));

            var lazyImport = container.GetExport<ExportWithLazyImport>();
            Assert.Equal(0, AnotherExport.ConstructionCount);
            Assert.False(lazyImport.AnotherExport.IsValueCreated);
            AnotherExport anotherExport = lazyImport.AnotherExport.Value;
            Assert.Equal(1, AnotherExport.ConstructionCount);

            // Verify that another instance gets its own instance of what it's importing (since it's non-shared).
            var lazyImport2 = container.GetExport<ExportWithLazyImport>();
            Assert.Equal(1, AnotherExport.ConstructionCount);
            Assert.False(lazyImport2.AnotherExport.IsValueCreated);
            AnotherExport anotherExport2 = lazyImport2.AnotherExport.Value;
            Assert.Equal(2, AnotherExport.ConstructionCount);
            Assert.NotSame(anotherExport, anotherExport2);
        }

        [Fact]
        public void LazyImportMany()
        {
            var container = TestUtilities.CreateContainer(typeof(ExportWithLazyImport), typeof(AnotherExport));
            var lazyImport = container.GetExport<ExportWithListOfLazyImport>();
            Assert.Equal(1, lazyImport.AnotherExports.Count);
            Assert.Equal(0, AnotherExport.ConstructionCount);
            Assert.False(lazyImport.AnotherExports[0].IsValueCreated);
            AnotherExport anotherExport = lazyImport.AnotherExports[0].Value;
            Assert.Equal(1, AnotherExport.ConstructionCount);
        }

        /// <summary>
        /// Verifies that the Lazy{T} instance itself is shared across all importers.
        /// </summary>
        [Fact]
        public void LazyImportOfSharedExportHasSharedLazy()
        {
            var container = TestUtilities.CreateContainer(typeof(ExportWithLazyImportOfSharedExport), typeof(SharedExport));
            var firstInstance = container.GetExport<ExportWithLazyImportOfSharedExport>();
            var secondInstance = container.GetExport<ExportWithLazyImportOfSharedExport>();
            Assert.NotSame(firstInstance, secondInstance); // We should get two copies of the non-shared instance

            // We're intentionally verifying the instance of the Lazy<T> *itself* (not its value).
            // We want it shared so that if any one service queries Lazy<T>.IsValueCreated, it will return true
            // if another other lazy importer evaluated it. Plus, as these Lazy's are thread-safe, there is some
            // sync object overhead that we'd rather minimize by sharing instances.
            Assert.Same(firstInstance.SharedExport, secondInstance.SharedExport);
        }

        [Fact]
        public void LazyImportOfSharedExportHasCreatedValueWhenCreatedByOtherMeans()
        {
            var container = TestUtilities.CreateContainer(typeof(ExportWithLazyImportOfSharedExport), typeof(SharedExport));

            var lazyImporter = container.GetExport<ExportWithLazyImportOfSharedExport>();
            Assert.False(lazyImporter.SharedExport.IsValueCreated);
            var sharedService = container.GetExport<SharedExport>();

            // This should be true, not because the lazyImporter instance evaluated the lazy,
            // but because this should reflect whether the service has actually been loaded.
            Assert.True(lazyImporter.SharedExport.IsValueCreated);
        }

        [Export]
        public class ExportWithLazyImport
        {
            [Import]
            public Lazy<AnotherExport> AnotherExport { get; set; }
        }

        [Export]
        public class ExportWithLazyImportOfSharedExport
        {
            [Import]
            public Lazy<SharedExport> SharedExport { get; set; }
        }

        [Export]
        public class ExportWithListOfLazyImport
        {
            [ImportMany]
            public List<Lazy<AnotherExport>> AnotherExports { get; set; }
        }

        [Export]
        public class AnotherExport
        {
            internal static int ConstructionCount;

            public AnotherExport()
            {
                ConstructionCount++;
            }
        }

        [Export, Shared]
        public class SharedExport { }
    }
}
