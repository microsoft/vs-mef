namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    internal static class TestUtilities
    {
        internal static CompositionContainer CreateContainer(this CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");
            return configuration.CreateContainerFactoryAsync().Result.CreateContainer();
        }

        internal static CompositionContainer CreateContainer(params Type[] parts)
        {
            return CompositionConfiguration.Create(parts).CreateContainer();
        }

        internal static IContainer CreateContainerV1(params Type[] parts)
        {
            Requires.NotNull(parts, "parts");
            var catalog = new MefV1.Hosting.TypeCatalog(parts);
            return CreateContainerV1(catalog);
        }

        internal static IContainer CreateContainerV1(ImmutableArray<Assembly> assemblies)
        {
            var catalog = new MefV1.Hosting.AggregateCatalog(assemblies.Select(a => new MefV1.Hosting.AssemblyCatalog(a)));
            return CreateContainerV1(catalog);
        }

        private static IContainer CreateContainerV1(MefV1.Primitives.ComposablePartCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");
            var container = new MefV1.Hosting.CompositionContainer(catalog, MefV1.Hosting.CompositionOptions.ExportCompositionService);
            return new V1ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV2(params Type[] parts)
        {
            var configuration = new ContainerConfiguration().WithParts(parts);
            return CreateContainerV2(configuration);
        }

        internal static IContainer CreateContainerV2(ImmutableArray<Assembly> assemblies)
        {
            var configuration = new ContainerConfiguration().WithAssemblies(assemblies);
            return CreateContainerV2(configuration);
        }

        private static IContainer CreateContainerV2(ContainerConfiguration configuration)
        {
            var container = configuration.CreateContainer();
            return new V2ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV3(params Type[] parts)
        {
            return CreateContainerV3(parts, CompositionEngines.Unspecified);
        }

        internal static IContainer CreateContainerV3(ImmutableArray<Assembly> assemblies)
        {
            return CreateContainerV3(assemblies, CompositionEngines.Unspecified);
        }

        internal static IContainer CreateContainerV3(Type[] parts, CompositionEngines attributesDiscovery)
        {
            PartDiscovery discovery = GetDiscoveryService(attributesDiscovery);
            var catalog = ComposableCatalog.Create(parts, discovery);
            return CreateContainerV3(catalog);
        }

        internal static IContainer CreateContainerV3(ImmutableArray<Assembly> assemblies, CompositionEngines attributesDiscovery)
        {
            PartDiscovery discovery = GetDiscoveryService(attributesDiscovery);
            var parts = discovery.CreateParts(assemblies);
            var catalog = ComposableCatalog.Create(parts);
            return CreateContainerV3(catalog);
        }

        private static PartDiscovery GetDiscoveryService(CompositionEngines attributesDiscovery)
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
            return discovery;
        }

        private static IContainer CreateContainerV3(ComposableCatalog catalog)
        {
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

        internal static void RunMultiEngineTest(CompositionEngines attributesVersion, ImmutableArray<Assembly> assemblies, Action<IContainer> test)
        {
            if (attributesVersion.HasFlag(CompositionEngines.V1))
            {
                test(CreateContainerV1(assemblies));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV1))
            {
                test(CreateContainerV3(assemblies, CompositionEngines.V1));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V2))
            {
                test(CreateContainerV2(assemblies));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV2))
            {
                test(CreateContainerV3(assemblies, CompositionEngines.V2));
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

            public ILazy<T> GetExport<T>()
            {
                return new LazyWrapper<T>(this.container.GetExport<T>());
            }

            public ILazy<T> GetExport<T>(string contractName)
            {
                return new LazyWrapper<T>(this.container.GetExport<T>(contractName));
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

            public ILazy<T> GetExport<T>()
            {
                // MEF v2 doesn't support this, so emulate it.
                return new LazyPart<T>(() => this.container.GetExport<T>());
            }

            public ILazy<T> GetExport<T>(string contractName)
            {
                // MEF v2 doesn't support this, so emulate it.
                return new LazyPart<T>(() => this.container.GetExport<T>(contractName));
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

            public ILazy<T> GetExport<T>()
            {
                return this.container.GetExport<T>();
            }

            public ILazy<T> GetExport<T>(string contractName)
            {
                return this.container.GetExport<T>(contractName);
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

        private class LazyWrapper<T> : ILazy<T>
        {
            private readonly Lazy<T> inner;

            internal LazyWrapper(Lazy<T> lazy)
            {
                Requires.NotNull(lazy, "lazy");
                this.inner = lazy;
            }

            public bool IsValueCreated
            {
                get { return this.inner.IsValueCreated; }
            }

            public T Value
            {
                get { return this.inner.Value; }
            }
        }
    }
}
