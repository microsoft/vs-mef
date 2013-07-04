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

        [Export]
        public class ExportWithLazyImport
        {
            [Import]
            public Lazy<AnotherExport> AnotherExport { get; set; }
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
    }
}
