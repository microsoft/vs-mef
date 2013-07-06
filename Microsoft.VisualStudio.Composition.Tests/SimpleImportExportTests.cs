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

    public class SimpleImportExportTests
    {
        [Fact]
        public void AcquireSingleExport()
        {
            TestUtilities.RunMultiEngineTest(
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
            TestUtilities.RunMultiEngineTest(
                new[] { typeof(Apple), typeof(Tree) },
                container =>
                {
                    Tree tree = container.GetExport<Tree>();
                    Assert.NotNull(tree);
                    Assert.NotNull(tree.Apple);
                });
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Apple
        {
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Tree
        {
            [Import]
            [MefV1.Import]
            public Apple Apple { get; set; }
        }
    }
}
