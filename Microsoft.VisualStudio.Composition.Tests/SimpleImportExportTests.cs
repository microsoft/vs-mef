using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Composition.Tests
{
    public class SimpleImportExportTests
    {
        [Fact]
        public void AcquireSingleExport()
        {
            TestUtilities.RunV2andV3CompatTest(
                new[] { typeof(Apple) },
                container =>
                {
                    Apple apple = container.GetExport<Apple>();
                    Assert.NotNull(apple);
                });
        }

        [Fact]
        public void AcquireExportWithImport()
        {
            TestUtilities.RunV2andV3CompatTest(
                new[] { typeof(Apple), typeof(Tree) },
                container =>
                {
                    Tree tree = container.GetExport<Tree>();
                    Assert.NotNull(tree);
                    Assert.NotNull(tree.Apple);
                });
        }

        [Export]
        public class Apple
        {
        }

        [Export]
        public class Tree
        {
            [Import]
            public Apple Apple { get; set; }
        }
    }
}
