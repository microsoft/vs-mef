// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Forest), typeof(Tree<>))]
        public void OpenAndClosedGenericExportsFromContainer(IContainer container)
        {
            var appleTrees = container.GetExportedValues<Tree<Apple>>().ToList();
            Assert.Equal(2, appleTrees.Count);

            var forestTree = appleTrees.Single(t => t is Forest.MyAppleTree);
            var loneTree = appleTrees.Single(t => !(t is Forest.MyAppleTree));

            var pearTrees = container.GetExportedValues<Tree<Pear>>().ToList();
            Assert.Equal(1, pearTrees.Count);
            Assert.NotNull(pearTrees[0]);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(Forest), typeof(Tree<>))]
        public void OpenAndClosedGenericExportsFromContainerWithMetadata(IContainer container)
        {
            var appleTrees = container.GetExports<Tree<Apple>, IDictionary<string, object>>().ToList();
            Assert.Equal(2, appleTrees.Count);

            var forestTree = appleTrees.Single(t => (string)t.Metadata["Origin"] == "Forest");
            var loneTree = appleTrees.Single(t => (string)t.Metadata["Origin"] == "Lone");

            Assert.IsType(typeof(Forest.MyAppleTree), forestTree.Value);
            Assert.IsType(typeof(Tree<Apple>), loneTree.Value);

            var pearTrees = container.GetExports<Tree<Pear>, IDictionary<string, object>>().ToList();
            Assert.Equal(1, pearTrees.Count);
            Assert.NotNull(pearTrees[0]);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Forest), typeof(Tree<>), typeof(ImportingPart))]
        public void OpenAndClosedGenericExportsFromPart(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal(2, part.AppleTrees.Length);

            var forestTree = part.AppleTrees.Single(t => (string)t.Metadata["Origin"] == "Forest");
            var loneTree = part.AppleTrees.Single(t => (string)t.Metadata["Origin"] == "Lone");

            Assert.IsType(typeof(Forest.MyAppleTree), forestTree.Value);
            Assert.IsType(typeof(Tree<Apple>), loneTree.Value);

            Assert.Equal(1, part.PearTrees.Length);
            Assert.NotNull(part.PearTrees[0]);
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
            public List<T>? Fruit { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class ImportingPart
        {
            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<Tree<Apple>, IDictionary<string, object>>[] AppleTrees { get; set; } = null!;

            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<Tree<Pear>, IDictionary<string, object>>[] PearTrees { get; set; } = null!;
        }

        public class Apple { }

        public class Pear { }
    }
}
