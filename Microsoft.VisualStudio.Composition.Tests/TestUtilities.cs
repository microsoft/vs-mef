namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class TestUtilities
    {
        internal static CompositionContainer CreateContainer(params Type[] parts)
        {
            return CompositionConfiguration.Create(parts).CreateContainer();
        }

        internal static IContainer CreateContainerV2(params Type[] parts)
        {
            var configuration = new ContainerConfiguration().WithParts(parts);
            var container = configuration.CreateContainer();
            return new V2ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV3(params Type[] parts)
        {
            var configuration = CompositionConfiguration.Create(parts);
            var container = configuration.CreateContainer();
            return new V3ContainerWrapper(container);
        }

        internal static void RunMultiEngineTest(Type[] parts, Action<IContainer> test)
        {
            test(CreateContainerV2(parts));
            test(CreateContainerV3(parts));
        }

        internal interface IContainer
        {
            T GetExport<T>();

            T GetExport<T>(string contractName);
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
