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
        [Flags]
        internal enum EngineAttributes
        {
            Unspecified = 0,
            V1,
            V2,
        }

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
            return CreateContainerV3(parts, EngineAttributes.Unspecified);
        }

        internal static IContainer CreateContainerV3(Type[] parts, EngineAttributes attributesDiscovery)
        {
            PartDiscovery discovery = null;
            if (attributesDiscovery.HasFlag(EngineAttributes.V1))
            {
                discovery = new AttributedPartDiscoveryV1();
            }
            else if (attributesDiscovery.HasFlag(EngineAttributes.V2))
            {
                discovery = new AttributedPartDiscovery();
            }

            var catalog = ComposableCatalog.Create(parts, discovery);
            var configuration = CompositionConfiguration.Create(catalog);
            var container = configuration.CreateContainer();
            return new V3ContainerWrapper(container);
        }

        internal static void RunMultiEngineTest(EngineAttributes attributesVersion, Type[] parts, Action<IContainer> test)
        {
            if (attributesVersion.HasFlag(EngineAttributes.V1))
            {
                // Run the test against System.ComponentModel.Composition.
                test(CreateContainerV1(parts));
                test(CreateContainerV3(parts, EngineAttributes.V1));
            }

            if (attributesVersion.HasFlag(EngineAttributes.V2))
            {
                // Run the test against System.Composition.
                test(CreateContainerV2(parts));
                test(CreateContainerV3(parts, EngineAttributes.V2));
            }
        }

        internal interface IContainer
        {
            T GetExport<T>();

            T GetExport<T>(string contractName);
        }

        private class V1ContainerWrapper : IContainer
        {
            private readonly MefV1.Hosting.CompositionContainer container;

            internal V1ContainerWrapper(MefV1.Hosting.CompositionContainer container)
            {
                Requires.NotNull(container, "container");
                this.container = container;
            }

            public T GetExport<T>()
            {
                return this.container.GetExportedValue<T>();
            }

            public T GetExport<T>(string contractName)
            {
                return this.container.GetExportedValue<T>(contractName);
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

            public T GetExport<T>()
            {
                return this.container.GetExport<T>();
            }

            public T GetExport<T>(string contractName)
            {
                return this.container.GetExport<T>(contractName);
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

            public T GetExport<T>()
            {
                return this.container.GetExport<T>();
            }

            public T GetExport<T>(string contractName)
            {
                return this.container.GetExport<T>(contractName);
            }
        }
    }
}
