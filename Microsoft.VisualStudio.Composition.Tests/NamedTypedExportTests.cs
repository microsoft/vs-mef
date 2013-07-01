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
        public NamedTypedExportTests()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(FruitTree));
            configurationBuilder.AddType(typeof(Apple));
            configurationBuilder.AddType(typeof(Pear));
            var configuration = configurationBuilder.CreateConfiguration();
            var containerFactory = configuration.CreateContainerFactoryAsync().Result;
            this.container = containerFactory.CreateContainer();
        }

        protected CompositionContainer container;

        [Fact]
        public void AcquireExportWithNamedImports()
        {
            FruitTree tree = this.container.GetExport<FruitTree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Pear);
            Assert.IsAssignableFrom(typeof(Pear), tree.Pear);
        }

        [Fact]
        public void AcquireNamedExport()
        {
            Fruit fruit = this.container.GetExport<Fruit>("Pear");
            Assert.NotNull(fruit);
            Assert.IsAssignableFrom(typeof(Pear), fruit);
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
