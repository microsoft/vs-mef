namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class NamedTypedExportTests
    {
        [Fact]
        public async Task AcquireExportWithNamedImports()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(FruitTree));
            configurationBuilder.AddType(typeof(Apple));
            configurationBuilder.AddType(typeof(Pear));
            var configuration = configurationBuilder.CreateConfiguration();
            var containerFactory = await configuration.CreateContainerFactoryAsync();
            var container = containerFactory.CreateContainer();
            FruitTree tree = container.GetExport<FruitTree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Pear);
            Assert.IsAssignableFrom(typeof(Pear), tree.Pear);
        }

        public class Fruit { }

        [Export("Pear", typeof(Fruit))]
        public class Pear : Fruit { }

        [Export(typeof(Fruit))]
        public class Apple : Fruit { }

        [Export]
        public class FruitTree
        {
            [Import("Pear")]
            public Fruit Pear { get; set; }
        }
    }
}
