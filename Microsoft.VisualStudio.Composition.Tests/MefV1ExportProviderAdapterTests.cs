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

    public class MefV1ExportProviderAdapterTests
    {
        // When we support this, we should have a flag that automatically creates an IContainer around a MEFv1 container
        // that uses a v3 export provider. That way we can just run ALL our tests through that mechanism to ensure
        // equivalent behavior between native V1 and emulated V1 through an adapting export provider.
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Apple), typeof(Tree), Skip = "Not yet working.")]
        public void GetSimpleExport(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var tree = v1Container.GetExportedValue<Tree>();
            Assert.NotNull(tree.Apple);
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Tree<>), Skip = "Not yet working.")]
        public void GetOpenGenericExport(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var tree = v1Container.GetExportedValue<Tree<int>>();
            Assert.NotNull(tree);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Apple
        {
            internal static int CreatedCount;

            public Apple()
            {
                CreatedCount++;
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Tree
        {
            [Import]
            [MefV1.Import]
            public Apple Apple { get; set; }
        }

        [Export(typeof(Tree<>))]
        [MefV1.Export(typeof(Tree<>)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Tree<T>
        {
        }
    }
}
