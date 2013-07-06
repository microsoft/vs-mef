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
        [CompatFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void AcquireSingleExport(IContainer container)
        {
            Apple apple = container.GetExport<Apple>();
            Assert.NotNull(apple);
        }

        [CompatFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void AcquireExportWithImport(IContainer container)
        {
            Tree tree = container.GetExport<Tree>();
            Assert.NotNull(tree);
            Assert.NotNull(tree.Apple);
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
