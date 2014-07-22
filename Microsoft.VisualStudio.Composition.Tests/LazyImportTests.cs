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
        public LazyImportTests()
        {
            AnotherExport.ConstructionCount = 0;
        }

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

        [MefFact(CompositionEngines.V2Compat, typeof(ExportWithLazyImportOfBaseType), typeof(AnotherExport))]
        public void LazyImportByBaseType(IContainer container)
        {
            var lazyImport = container.GetExportedValue<ExportWithLazyImportOfBaseType>();
            Assert.IsType(typeof(AnotherExport), lazyImport.AnotherExport.Value);
        }

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

        /// <summary>
        /// Verifies that the Lazy{T} instance itself is shared across all importers.
        /// </summary>
        /// <remarks>
        /// This design goal has been retired. There are too many other concerns that are more impactful,
        /// and getting this just right is quite tricky, and often Lazy's really can't be shared
        /// across importers for various reasons.
        /// </remarks>
        ////[MefFact(CompositionEngines.Unspecified, typeof(ExportWithLazyImportOfSharedExport), typeof(SharedExport))]
        [Trait("Efficiency", "InstanceReuse")]
        public void LazyImportOfSharedExportHasSharedLazy(IContainer container)
        {
            var firstInstance = container.GetExportedValue<ExportWithLazyImportOfSharedExport>();
            var secondInstance = container.GetExportedValue<ExportWithLazyImportOfSharedExport>();
            Assert.NotSame(firstInstance, secondInstance); // We should get two copies of the non-shared instance
            Assert.Same(firstInstance.SharedExport.Value, secondInstance.SharedExport.Value);

            // We're intentionally verifying the instance of the Lazy<T> *itself* (not its value).
            // We want it shared so that if any one service queries Lazy<T>.IsValueCreated, it will return true
            // if another other lazy importer evaluated it. Plus, as these Lazy's are thread-safe, there is some
            // sync object overhead that we'd rather minimize by sharing instances.
            Assert.Same(firstInstance.SharedExport, secondInstance.SharedExport);
        }

        [Fact(Skip = "Functionality not yet implemented.")]
        public void LazyImportOfSharedExportHasCreatedValueWhenCreatedByOtherMeans()
        {
            var container = TestUtilities.CreateContainer(typeof(ExportWithLazyImportOfSharedExport), typeof(SharedExport));

            var lazyImporter = container.GetExportedValue<ExportWithLazyImportOfSharedExport>();
            Assert.False(lazyImporter.SharedExport.IsValueCreated);
            var sharedService = container.GetExportedValue<SharedExport>();

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
            public IList<Lazy<AnotherExport>> AnotherExports { get; set; }
        }

        [Export]
        public class ExportWithLazyImportOfBaseType
        {
            [Import("AnotherExport")]
            public Lazy<object> AnotherExport { get; set; }
        }

        [Export]
        [Export("AnotherExport", typeof(object))]
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
