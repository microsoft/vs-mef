namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    internal static class TestUtilities
    {
        internal static CompositionContainer CreateContainer(params Type[] parts)
        {
            return CompositionConfiguration.Create(parts).CreateContainer();
        }

        internal static IContainer CreateContainerV1(params Type[] parts)
        {
            var catalog = new MefV1.Hosting.TypeCatalog(parts);
            var container = new MefV1.Hosting.CompositionContainer(catalog);
            return new V1ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV2(params Type[] parts)
        {
            var configuration = new ContainerConfiguration().WithParts(parts);
            var container = configuration.CreateContainer();
            return new V2ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV3(params Type[] parts)
        {
            return CreateContainerV3(parts, CompositionEngines.Unspecified);
        }

        internal static IContainer CreateContainerV3(Type[] parts, CompositionEngines attributesDiscovery)
        {
            PartDiscovery discovery = null;
            if (attributesDiscovery.HasFlag(CompositionEngines.V1))
            {
                discovery = new AttributedPartDiscoveryV1();
            }
            else if (attributesDiscovery.HasFlag(CompositionEngines.V2))
            {
                discovery = new AttributedPartDiscovery();
            }

            var catalog = ComposableCatalog.Create(parts, discovery);
            var configuration = CompositionConfiguration.Create(catalog);
            var container = configuration.CreateContainer();
            return new V3ContainerWrapper(container);
        }

        internal static void RunMultiEngineTest(CompositionEngines attributesVersion, Type[] parts, Action<IContainer> test)
        {
            if (attributesVersion.HasFlag(CompositionEngines.V1))
            {
                test(CreateContainerV1(parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV1))
            {
                test(CreateContainerV3(parts, CompositionEngines.V1));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V2))
            {
                test(CreateContainerV2(parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV2))
            {
                test(CreateContainerV3(parts, CompositionEngines.V2));
            }

        }

        private class V1ContainerWrapper : IContainer
        {
            private readonly MefV1.Hosting.CompositionContainer container;

            internal V1ContainerWrapper(MefV1.Hosting.CompositionContainer container)
            {
                Requires.NotNull(container, "container");
                this.container = container;
            }

            public T GetExportedValue<T>()
            {
                return this.container.GetExportedValue<T>();
            }

            public T GetExportedValue<T>(string contractName)
            {
                return this.container.GetExportedValue<T>(contractName);
            }

            public void Dispose()
            {
                this.container.Dispose();
            }
        }


        private class V2ContainerWrapper : IContainer
        {
            private readonly CompositionHost container;

            internal V2ContainerWrapper(CompositionHost container)
            {
                Requires.NotNull(container, "container");
                this.container = container;
            }

            public T GetExportedValue<T>()
            {
                return this.container.GetExport<T>();
            }

            public T GetExportedValue<T>(string contractName)
            {
                return this.container.GetExport<T>(contractName);
            }

            public void Dispose()
            {
                this.container.Dispose();
            }
        }

        private class V3ContainerWrapper : IContainer
        {
            private readonly CompositionContainer container;

            internal V3ContainerWrapper(CompositionContainer container)
            {
                Requires.NotNull(container, "container");
                this.container = container;
            }

            public T GetExportedValue<T>()
            {
                return this.container.GetExportedValue<T>();
            }

            public T GetExportedValue<T>(string contractName)
            {
                return this.container.GetExportedValue<T>(contractName);
            }

            public void Dispose()
            {
                this.container.Dispose();
            }
        }
    }
}
