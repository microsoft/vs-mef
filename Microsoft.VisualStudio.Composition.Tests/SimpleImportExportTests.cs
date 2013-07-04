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
        public void AcquireSingleExportv2()
        {
            var configuration = new ContainerConfiguration()
                .WithPart(typeof(Apple));
            var container = configuration.CreateContainer();
            Apple apple = container.GetExport<Apple>();
            Assert.NotNull(apple);
        }

        [Fact]
        public void AcquireSingleExportv3()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(Apple));
            var configuration = configurationBuilder.CreateConfiguration();
            var container = configuration.CreateContainer();
            Apple apple = container.GetExport<Apple>();
            Assert.NotNull(apple);
        }

        [Fact]
        public void AcquireExportWithImportv2()
        {
            var configuration = new ContainerConfiguration()
                .WithPart(typeof(Apple))
                .WithPart(typeof(Tree));
            CompositionHost container = configuration.CreateContainer();
            Tree tree = container.GetExport<Tree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Apple);
        }

        [Fact]
        public async Task AcquireExportWithImportv3()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(Apple));
            configurationBuilder.AddType(typeof(Tree));
            var configuration = configurationBuilder.CreateConfiguration();
            var container = configuration.CreateContainer();
            Tree tree = container.GetExport<Tree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Apple);
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
