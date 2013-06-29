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
            var configuration = new ContainerConfiguration()
                .WithPart(typeof(Apple));
            var container = configuration.CreateContainer();
            Apple apple = container.GetExport<Apple>();
            Assert.NotNull(apple);
        }

        [Fact]
        public void AcquireExportWithImport()
        {
            var configuration = new ContainerConfiguration()
                .WithPart(typeof(Apple))
                .WithPart(typeof(Tree));
            var container = configuration.CreateContainer();
            Tree tree = container.GetExport<Tree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Apple);
        }
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
