
namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class NamedTypedExportTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void AcquireExportWithNamedImports(IContainer container)
        {
            FruitTree tree = container.GetExportedValue<FruitTree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Pear);
            Assert.IsAssignableFrom(typeof(Pear), tree.Pear);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void AcquireNamedExport(IContainer container)
        {
            Fruit fruit = container.GetExportedValue<Fruit>("Pear");
            Assert.NotNull(fruit);
            Assert.IsAssignableFrom(typeof(Pear), fruit);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportsNamed(IContainer container)
        {
            IEnumerable<ILazy<Fruit>> result = container.GetExports<Fruit>("Pear");
            Assert.Equal(1, result.Count());
            Assert.IsType<Pear>(result.Single().Value);
        }

        public class Fruit { }

        [Export("Pear", typeof(Fruit))]
        [MefV1.Export("Pear", typeof(Fruit))]
        public class Pear : Fruit { }

        [Export(typeof(Fruit))]
        [MefV1.Export(typeof(Fruit))]
        public class Apple : Fruit { }

        [Export]
        [MefV1.Export]
        public class FruitTree
        {
            [Import("Pear")]
            [MefV1.Import("Pear")]
            public Fruit Pear { get; set; }
        }
    }
}
