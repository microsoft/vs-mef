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

        internal static void RunV2andV3CompatTest(Type[] parts, Action<IContainer> test)
        {
            var v2configuration = new ContainerConfiguration().WithParts(parts);
            var v3configuration = CompositionConfiguration.Create(parts);

            CompositionHost v2container = v2configuration.CreateContainer();
            CompositionContainer v3container = v3configuration.CreateContainer();

            test(new V2ContainerWrapper(v2container));
            test(new V3ContainerWrapper(v3container));
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
