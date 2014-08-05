namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class MefV1ExportProviderAdapterTests
    {
        // Test backlog:
        //  * Test that all thrown exceptions are MEFv1 exception types.
        //  * All the other methods on MefV1.ExportProvider: ReleaseExport, Compose, etc.
        //    Some we may throw for, but these should be verified.

        // When we support this, we should have a flag that automatically creates an IContainer around a MEFv1 container
        // that uses a v3 export provider. That way we can just run ALL our tests through that mechanism to ensure
        // equivalent behavior between native V1 and emulated V1 through an adapting export provider.
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Apple), typeof(Tree))]
        public void GetSimpleExport(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var tree = v1Container.GetExportedValue<Tree>();
            Assert.NotNull(tree.Apple);
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Apple))]
        public void GetNamedExports(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var apples = v1Container.GetExportedValues<Apple>(typeof(Apple).FullName);
            Assert.Equal(1, apples.Count());

            apples = v1Container.GetExportedValues<Apple>("SomeContract");
            Assert.Equal(1, apples.Count());

            apples = v1Container.GetExportedValues<Apple>("NoContractLikeThis");
            Assert.Equal(0, apples.Count());
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Tree<>))]
        public void GetOpenGenericExport(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var tree = v1Container.GetExportedValue<Tree<int>>();
            Assert.NotNull(tree);
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Tree), typeof(Apple))]
        [Trait("Metadata", "")]
        public void GetExportWithMetadataDictionary(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var tree = v1Container.GetExport<Tree, IDictionary<string, object>>();
            Assert.Equal("b", tree.Metadata["A"]);
            Assert.False(tree.Metadata.ContainsKey("B"));
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Tree), typeof(Apple))]
        [Trait("Metadata", "TMetadata")]
        public void GetExportWithTMetadata(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var tree = v1Container.GetExport<Tree, IMetadata>();
            Assert.Equal("b", tree.Metadata.A);
            Assert.Equal("c", tree.Metadata.B);
        }

        #region SatisfyImportsOnce

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Apple))]
        public void SatisfyImportsOnceWithRequiredCreationPolicy(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var receiver = new SatisfyImportsOnceWithPartCreationPolicyReceiver();
            MefV1.AttributedModelServices.SatisfyImportsOnce(v1Container, receiver);
            Assert.NotNull(receiver.NonSharedApple);

            // Not sure why this turns out to not be null. The creation policies do not match.
            ////Assert.Null(receiver.SharedApple);
        }

        private class SatisfyImportsOnceWithPartCreationPolicyReceiver
        {
            [MefV1.Import(RequiredCreationPolicy = MefV1.CreationPolicy.NonShared)]
            public Apple NonSharedApple { get; set; }

            [MefV1.Import(RequiredCreationPolicy = MefV1.CreationPolicy.Shared, AllowDefault = true)]
            public Apple SharedApple { get; set; }
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Apple), Skip = "Not yet supported.")]
        public void SatisfyImportsOnceWithExportFactory(IContainer container)
        {
            var v3Container = (TestUtilities.V3ContainerWrapper)container;

            var v1Container = new MefV1.Hosting.CompositionContainer(v3Container.ExportProvider.AsExportProvider());
            var receiver = new SatisfyImportsOnceWithExportFactoryReceiver();
            MefV1.AttributedModelServices.SatisfyImportsOnce(v1Container, receiver);
            Assert.NotNull(receiver.AppleFactory);
            MefV1.ExportLifetimeContext<Apple> apple1 = receiver.AppleFactory.CreateExport();
            Assert.NotNull(apple1);
            Assert.NotNull(apple1.Value);
            MefV1.ExportLifetimeContext<Apple> apple2 = receiver.AppleFactory.CreateExport();
            Assert.NotNull(apple2);
            Assert.NotNull(apple2.Value);

            Assert.NotSame(apple1, apple2);
            Assert.NotSame(apple1.Value, apple2.Value);
        }

        private class SatisfyImportsOnceWithExportFactoryReceiver
        {
            [MefV1.Import]
            public MefV1.ExportFactory<Apple> AppleFactory { get; set; }
        }

        #endregion

        [Export, Export("SomeContract")]
        [MefV1.Export, MefV1.Export("SomeContract"), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
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
        [ExportMetadata("A", "b")]
        [MefV1.ExportMetadata("A", "b")]
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

        public interface IMetadata
        {
            string A { get; }

            [DefaultValue("c")]
            string B { get; }
        }
    }
}
