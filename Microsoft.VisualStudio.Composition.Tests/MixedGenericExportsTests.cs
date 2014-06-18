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

    [Trait("GenericExports", "Closed")]
    [Trait("GenericExports", "Open")]
    public class MixedGenericExportsTests
    {
        [MefFact(CompositionEngines.V1/*Compat*/ | CompositionEngines.V2/*Compat*/, typeof(Forest), typeof(Tree<>))]
        public void OpenAndClosedGenericExportsFromContainer(IContainer container)
        {
            var trees = container.GetExportedValues<Tree<Apple>>().ToList();
            Assert.Equal(2, trees.Count);

            var forestTree = trees.Single(t => t is Forest.MyAppleTree);
            var loneTree = trees.Single(t => !(t is Forest.MyAppleTree));
        }

        [MefFact(CompositionEngines.V1/*Compat | CompositionEngines.V3EmulatingV2*/, typeof(Forest), typeof(Tree<>))]
        public void OpenAndClosedGenericExportsFromContainerWithMetadata(IContainer container)
        {
            var trees = container.GetExports<Tree<Apple>, IDictionary<string, object>>().ToList();
            Assert.Equal(2, trees.Count);

            var forestTree = trees.Single(t => "Forest" == (string)t.Metadata["Origin"]);
            var loneTree = trees.Single(t => "Lone" == (string)t.Metadata["Origin"]);

            Assert.IsType(typeof(Forest.MyAppleTree), forestTree.Value);
            Assert.IsType(typeof(Tree<Apple>), loneTree.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Forest), typeof(Tree<>), typeof(ImportingPart))]
        public void OpenAndClosedGenericExportsFromPart(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.NotNull(part);
            Assert.NotNull(part.AppleTrees);
            Assert.Equal(2, part.AppleTrees.Length);

            var forestTree = part.AppleTrees.Single(t => "Forest" == (string)t.Metadata["Origin"]);
            var loneTree = part.AppleTrees.Single(t => "Lone" == (string)t.Metadata["Origin"]);

            Assert.IsType(typeof(Forest.MyAppleTree), forestTree.Value);
            Assert.IsType(typeof(Tree<Apple>), loneTree.Value);
        }

        [Shared]
        public class Forest
        {
            [Export, ExportMetadata("Origin", "Forest")]
            [MefV1.Export, MefV1.ExportMetadata("Origin", "Forest")]
            public Tree<Apple> AppleTree
            {
                get { return new MyAppleTree(); }
            }

            internal class MyAppleTree : Tree<Apple> { }
        }

        [Export, ExportMetadata("Origin", "Lone"), Shared]
        [MefV1.Export, MefV1.ExportMetadata("Origin", "Lone")]
        public class Tree<T>
        {
            public List<T> Fruit { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class ImportingPart
        {
            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<Tree<Apple>, IDictionary<string, object>>[] AppleTrees { get; set; }
        }

        public class Apple { }
    }
}
