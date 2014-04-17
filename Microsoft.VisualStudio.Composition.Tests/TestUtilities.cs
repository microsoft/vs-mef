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
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;

    internal static class TestUtilities
    {
        internal static CompositionContainer CreateContainer(this CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");
            return configuration.CreateContainerFactoryAsync(Console.Out, Console.Out).Result.CreateContainer();
        }

        internal static CompositionContainer CreateContainer(params Type[] parts)
        {
            return CompositionConfiguration.Create(new AttributedPartDiscovery(), parts).CreateContainer();
        }

        internal static IContainer CreateContainerV1(params Type[] parts)
        {
            Requires.NotNull(parts, "parts");
            var catalog = new MefV1.Hosting.TypeCatalog(parts);
            return CreateContainerV1(catalog);
        }

        internal static IContainer CreateContainerV1(IReadOnlyList<Assembly> assemblies, Type[] parts)
        {
            Requires.NotNull(parts, "parts");
            var catalogs = assemblies.Select(a => new MefV1.Hosting.AssemblyCatalog(a))
                .Concat<MefV1.Primitives.ComposablePartCatalog>(new[] { new MefV1.Hosting.TypeCatalog(parts) });
            var catalog = new MefV1.Hosting.AggregateCatalog(catalogs);

            return CreateContainerV1(catalog);
        }

        private static IContainer CreateContainerV1(MefV1.Primitives.ComposablePartCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");
            var container = new DebuggableCompositionContainer(catalog, MefV1.Hosting.CompositionOptions.ExportCompositionService);
            return new V1ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV2(params Type[] parts)
        {
            var configuration = new ContainerConfiguration().WithParts(parts);
            return CreateContainerV2(configuration);
        }

        internal static IContainer CreateContainerV2(IReadOnlyList<Assembly> assemblies, Type[] types)
        {
            var configuration = new ContainerConfiguration().WithAssemblies(assemblies).WithParts(types);
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

        internal static IContainer CreateContainerV3(IReadOnlyList<Assembly> assemblies)
        {
            return CreateContainerV3(assemblies, CompositionEngines.Unspecified);
        }

        internal static IContainer CreateContainerV3(Type[] parts, CompositionEngines attributesDiscovery)
        {
            return CreateContainerV3(default(IReadOnlyList<Assembly>), attributesDiscovery, parts);
        }

        internal static IContainer CreateContainerV3(IReadOnlyList<Assembly> assemblies, CompositionEngines attributesDiscovery, Type[] parts = null)
        {
            PartDiscovery discovery = GetDiscoveryService(attributesDiscovery);
            var assemblyParts = discovery.CreateParts(assemblies);
            var catalog = ComposableCatalog.Create(assemblyParts);
            if (parts != null && parts.Length != 0)
            {
                var typeCatalog = ComposableCatalog.Create(discovery, parts);
                catalog = ComposableCatalog.Create(catalog.Parts.Concat(typeCatalog.Parts));
            }
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
                var v2Discovery = new AttributedPartDiscovery();
                if (attributesDiscovery.HasFlag(CompositionEngines.V3NonPublicSupport))
                {
                    v2Discovery.IsNonPublicSupported = true;
                }

                discovery = v2Discovery;
            }
            return discovery;
        }

        private static IContainer CreateContainerV3(ComposableCatalog catalog)
        {
            var configuration = CompositionConfiguration.Create(catalog);
#if DGML
            string dgmlFile = System.IO.Path.GetTempFileName() + ".dgml";
            configuration.CreateDgml().Save(dgmlFile);
            Console.WriteLine("DGML saved to: " + dgmlFile);
#endif
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

        internal static void RunMultiEngineTest(CompositionEngines attributesVersion, IReadOnlyList<Assembly> assemblies, Type[] parts, Action<IContainer> test)
        {
            if (attributesVersion.HasFlag(CompositionEngines.V1))
            {
                test(CreateContainerV1(assemblies, parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV1))
            {
                test(CreateContainerV3(assemblies, CompositionEngines.V1, parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V2))
            {
                test(CreateContainerV2(assemblies, parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV2))
            {
                test(CreateContainerV3(assemblies, CompositionEngines.V2 | (CompositionEngines.V3NonPublicSupport & attributesVersion), parts));
            }
        }

        private class DebuggableCompositionContainer : MefV1.Hosting.CompositionContainer
        {
            protected override IEnumerable<MefV1.Primitives.Export> GetExportsCore(MefV1.Primitives.ImportDefinition definition, MefV1.Hosting.AtomicComposition atomicComposition)
            {
                var result = base.GetExportsCore(definition, atomicComposition);
                if ((definition.Cardinality == MefV1.Primitives.ImportCardinality.ExactlyOne && result.Count() != 1) ||
                    (definition.Cardinality == MefV1.Primitives.ImportCardinality.ZeroOrOne && result.Count() > 1))
                {
                    // Set breakpoint here
                }
                return result;
            }

            public DebuggableCompositionContainer(MefV1.Primitives.ComposablePartCatalog catalog, MefV1.Hosting.CompositionOptions compositionOptions)
                : base(catalog, compositionOptions)
            {
            }
        }

        private class V1ContainerWrapper : IContainer
        {
            private readonly DebuggableCompositionContainer container;

            public DebuggableCompositionContainer Container
            {
                get { return container; }
            }

            internal V1ContainerWrapper(DebuggableCompositionContainer container)
            {
                Requires.NotNull(container, "container");
                this.container = container;
            }

            public ILazy<T> GetExport<T>()
            {
                try
                {
                    return new LazyWrapper<T>(this.container.GetExport<T>());
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public ILazy<T> GetExport<T>(string contractName)
            {
                try
                {
                    return new LazyWrapper<T>(this.container.GetExport<T>(contractName));
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public ILazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                try
                {
                    return new LazyWrapper<T, TMetadataView>(this.container.GetExport<T, TMetadataView>());
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
            {
                try
                {
                    return new LazyWrapper<T, TMetadataView>(this.container.GetExport<T, TMetadataView>(contractName));
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T>> GetExports<T>()
            {
                try
                {
                    return this.container.GetExports<T>().Select(l => new LazyWrapper<T>(l));
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T>> GetExports<T>(string contractName)
            {
                try
                {
                    return this.container.GetExports<T>(contractName).Select(l => new LazyWrapper<T>(l));
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                try
                {
                    return this.container.GetExports<T, TMetadataView>().Select(l => (ILazy<T, TMetadataView>)new LazyWrapper<T, TMetadataView>(l));
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
            {
                try
                {
                    return this.container.GetExports<T, TMetadataView>(contractName).Select(l => (ILazy<T, TMetadataView>)new LazyWrapper<T, TMetadataView>(l));
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public T GetExportedValue<T>()
            {
                try
                {
                    return this.container.GetExportedValue<T>();
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public T GetExportedValue<T>(string contractName)
            {
                try
                {
                    return this.container.GetExportedValue<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<T> GetExportedValues<T>()
            {
                try
                {
                    return this.container.GetExportedValues<T>();
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<T> GetExportedValues<T>(string contractName)
            {
                try
                {
                    return this.container.GetExportedValues<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
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
                return new LazyPart<T>(() =>
                {
                    try
                    {
                        return this.container.GetExport<T>();
                    }
                    catch (System.Composition.Hosting.CompositionFailedException ex)
                    {
                        throw new CompositionFailedException(ex.Message, ex);
                    }
                });
            }

            public ILazy<T> GetExport<T>(string contractName)
            {
                // MEF v2 doesn't support this, so emulate it.
                return new LazyPart<T>(() =>
                {
                    try
                    {
                        return this.container.GetExport<T>(contractName);
                    }
                    catch (System.Composition.Hosting.CompositionFailedException ex)
                    {
                        throw new CompositionFailedException(ex.Message, ex);
                    }
                });
            }

            public ILazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                throw new NotImplementedException();
            }

            public ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
            {
                throw new NotImplementedException();
            }

            public T GetExportedValue<T>()
            {
                try
                {
                    return this.container.GetExport<T>();
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public T GetExportedValue<T>(string contractName)
            {
                try
                {
                    return this.container.GetExport<T>(contractName);
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T>> GetExports<T>()
            {
                try
                {
                    return this.container.GetExports<T>().Select(v => LazyPart.Wrap(v));
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T>> GetExports<T>(string contractName)
            {
                try
                {
                    return this.container.GetExports<T>(contractName).Select(v => LazyPart.Wrap(v));
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                throw new NotSupportedException();
            }

            public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
            {
                throw new NotSupportedException();
            }

            public IEnumerable<T> GetExportedValues<T>()
            {
                try
                {
                    return this.container.GetExports<T>();
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<T> GetExportedValues<T>(string contractName)
            {
                try
                {
                    return this.container.GetExports<T>(contractName);
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
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

            public ILazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                return this.container.GetExport<T, TMetadataView>();
            }

            public ILazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
            {
                return this.container.GetExport<T, TMetadataView>(contractName);
            }

            public T GetExportedValue<T>()
            {
                return this.container.GetExportedValue<T>();
            }

            public T GetExportedValue<T>(string contractName)
            {
                return this.container.GetExportedValue<T>(contractName);
            }

            public IEnumerable<ILazy<T>> GetExports<T>()
            {
                return this.container.GetExports<T>();
            }

            public IEnumerable<ILazy<T>> GetExports<T>(string contractName)
            {
                return this.container.GetExports<T>(contractName);
            }

            public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                return this.container.GetExports<T, TMetadataView>();
            }

            public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
            {
                return this.container.GetExports<T, TMetadataView>(contractName);
            }

            public IEnumerable<T> GetExportedValues<T>()
            {
                return this.container.GetExportedValues<T>();
            }

            public IEnumerable<T> GetExportedValues<T>(string contractName)
            {
                return this.container.GetExportedValues<T>(contractName);
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

        private class LazyWrapper<T, TMetadata> : ILazy<T, TMetadata>, ILazy<T>
        {
            private readonly Lazy<T, TMetadata> inner;

            internal LazyWrapper(Lazy<T, TMetadata> lazy)
            {
                Requires.NotNull(lazy, "lazy");
                this.inner = lazy;
            }

            public bool IsValueCreated
            {
                get { return this.inner.IsValueCreated; }
            }

            public TMetadata Metadata
            {
                get { return this.inner.Metadata; }
            }

            public T Value
            {
                get { return this.inner.Value; }
            }
        }
    }
}
