
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
        [MefFact(CompositionEngines.V2Compat)]
        public void AcquireExportWithNamedImports(IContainer container)
        {
            FruitTree tree = container.GetExportedValue<FruitTree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Pear);
            Assert.IsAssignableFrom(typeof(Pear), tree.Pear);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void AcquireNamedExport(IContainer container)
        {
            Fruit fruit = container.GetExportedValue<Fruit>("Pear");
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
