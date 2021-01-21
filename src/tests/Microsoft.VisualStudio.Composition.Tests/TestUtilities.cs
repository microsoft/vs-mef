// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition.Hosting;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;
    using MefV1 = System.ComponentModel.Composition;

    internal static class TestUtilities
    {
        /// <summary>
        /// Gets the timeout value to use for tests that do not expect the timeout to occur.
        /// </summary>
        internal static TimeSpan UnexpectedTimeout
        {
            get
            {
                return Debugger.IsAttached
                    ? Timeout.InfiniteTimeSpan
                    : TimeSpan.FromSeconds(2);
            }
        }

        /// <summary>
        /// Gets a timeout value to use for tests that expect the timeout to occur.
        /// </summary>
        internal static TimeSpan ExpectedTimeout
        {
            get
            {
                return Debugger.IsAttached
                    ? TimeSpan.FromSeconds(5)
                    : TimeSpan.FromMilliseconds(200);
            }
        }

        internal static Resolver Resolver = Resolver.DefaultInstance;

        internal static ComposableCatalog EmptyCatalog = ComposableCatalog.Create(Resolver);

        internal static PartDiscovery V1Discovery = new AttributedPartDiscoveryV1(Resolver);

        internal static AttributedPartDiscovery V2Discovery = new AttributedPartDiscovery(Resolver);

        internal static AttributedPartDiscovery V2DiscoveryWithNonPublics = new AttributedPartDiscovery(Resolver, isNonPublicSupported: true);

        internal static async Task<ExportProvider> CreateContainerAsync(this CompositionConfiguration configuration, ITestOutputHelper output)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(output, nameof(output));

            var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);

            // Round-trip serialization to make sure the result is equivalent.
            var cacheManager = new CachedComposition();
            var ms = new MemoryStream();
            await cacheManager.SaveAsync(runtimeComposition, ms);
            output.WriteLine("Cache file size: {0}", ms.Length);
            ms.Position = 0;
            var deserializedRuntimeComposition = await cacheManager.LoadRuntimeCompositionAsync(ms, Resolver);
            Assert.Equal(runtimeComposition, deserializedRuntimeComposition);

            return runtimeComposition.CreateExportProviderFactory().CreateExportProvider();
        }

        internal static IContainer CreateContainerV1(IReadOnlyList<Assembly> assemblies, Type[] parts)
        {
            Requires.NotNull(parts, nameof(parts));
            var catalogs = assemblies.Select(a => new MefV1.Hosting.AssemblyCatalog(a))
                .Concat<MefV1.Primitives.ComposablePartCatalog>(new[] { new MefV1.Hosting.TypeCatalog(parts) });
            var catalog = new MefV1.Hosting.AggregateCatalog(catalogs);

            return CreateContainerV1(catalog);
        }

        private static IContainer CreateContainerV1(MefV1.Primitives.ComposablePartCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));
            var container = new DebuggableCompositionContainer(catalog, MefV1.Hosting.CompositionOptions.ExportCompositionService | MefV1.Hosting.CompositionOptions.IsThreadSafe);
            return new V1ContainerWrapper(container);
        }

        internal static IContainer CreateContainerV2(IReadOnlyList<Assembly> assemblies, Type[] types)
        {
            var configuration = new ContainerConfiguration().WithAssemblies(assemblies).WithParts(types);
            return CreateContainerV2(configuration);
        }

        private static IContainer CreateContainerV2(ContainerConfiguration configuration)
        {
            try
            {
                var container = configuration.CreateContainer();
                return new V2ContainerWrapper(container);
            }
            catch (System.Composition.Hosting.CompositionFailedException ex)
            {
                throw new CompositionFailedException(ex.Message, ex);
            }
        }

        internal static async Task<CompositionConfiguration> CreateConfigurationAsync(CompositionEngines attributesDiscovery, params Type[] parts)
        {
            PartDiscovery discovery = GetDiscoveryService(attributesDiscovery);
            var assemblyParts = await discovery.CreatePartsAsync(parts);
            var catalog = EmptyCatalog.AddParts(assemblyParts);
            return CompositionConfiguration.Create(catalog);
        }

        [return: NotNullIfNotNull("ex")]
        internal static Exception? GetInnermostException(Exception? ex)
        {
            while (ex?.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }

        private static PartDiscovery GetDiscoveryService(CompositionEngines attributesDiscovery)
        {
            var discovery = new List<PartDiscovery>(2);
            if (attributesDiscovery.HasFlag(CompositionEngines.V1))
            {
                discovery.Add(V1Discovery);
            }

            if (attributesDiscovery.HasFlag(CompositionEngines.V2))
            {
                var v2Discovery = attributesDiscovery.HasFlag(CompositionEngines.V3NonPublicSupport)
                    ? V2DiscoveryWithNonPublics
                    : V2Discovery;
                discovery.Add(v2Discovery);
            }

            return PartDiscovery.Combine(discovery.ToArray());
        }

        /// <summary>
        /// Gets a value indicating whether the test is running on the Mono runtime.
        /// </summary>
        internal static bool IsOnMono => Type.GetType("Mono.Runtime") != null;

        /// <summary>
        /// Gets a value indicating whether the test is running on the CoreCLR runtime.
        /// </summary>
        internal static bool IsOnCoreCLR
        {
            get
            {
#if NETCOREAPP
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Causes a <see cref="SkippableFactAttribute"/> based test to skip if <see cref="IsOnMono"/>.
        /// </summary>
        internal static void SkipOnMono(string unsupportedFeature)
        {
            Skip.If(IsOnMono, "Test marked as skipped on Mono runtime due to feature: " + unsupportedFeature);
        }

        internal class DebuggableCompositionContainer : MefV1.Hosting.CompositionContainer
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

        internal class V1ContainerWrapper : IContainer
        {
            private readonly MefV1.Hosting.CompositionContainer container;

            internal MefV1.Hosting.CompositionContainer Container
            {
                get { return this.container; }
            }

            internal V1ContainerWrapper(MefV1.Hosting.CompositionContainer container)
            {
                Requires.NotNull(container, nameof(container));
                this.container = container;
            }

            public Lazy<T> GetExport<T>()
            {
                try
                {
                    return this.container.GetExport<T>();
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public Lazy<T> GetExport<T>(string? contractName)
            {
                try
                {
                    return this.container.GetExport<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                try
                {
                    return this.container.GetExport<T, TMetadataView>();
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string? contractName)
            {
                try
                {
                    return this.container.GetExport<T, TMetadataView>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<T>> GetExports<T>()
            {
                try
                {
                    return this.container.GetExports<T>();
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<T>> GetExports<T>(string? contractName)
            {
                try
                {
                    return this.container.GetExports<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                try
                {
                    return this.container.GetExports<T, TMetadataView>();
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string? contractName)
            {
                try
                {
                    return this.container.GetExports<T, TMetadataView>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<object?, object>> GetExports(Type type, Type metadataViewType, string? contractName) => this.container.GetExports(type, metadataViewType, contractName);

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
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public T GetExportedValue<T>(string? contractName)
            {
                try
                {
                    return this.container.GetExportedValue<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
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
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<T> GetExportedValues<T>(string? contractName)
            {
                try
                {
                    return this.container.GetExportedValues<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
                catch (MefV1.CompositionException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<object> GetExportedValues(Type type, string? contractName) => throw new NotSupportedException();

            public void ReleaseExport<T>(Lazy<T> export) => this.container.ReleaseExport(export);

            public void ReleaseExports<T>(IEnumerable<Lazy<T>> export) => this.container.ReleaseExports(export);

            public void ReleaseExports<T, TMetadataView>(IEnumerable<Lazy<T, TMetadataView>> export) => this.container.ReleaseExports(export);

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
                Requires.NotNull(container, nameof(container));
                this.container = container;
            }

            public Lazy<T> GetExport<T>()
            {
                // MEF v2 doesn't support this, so emulate it.
                return new Lazy<T>(() =>
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

            public Lazy<T> GetExport<T>(string? contractName)
            {
                // MEF v2 doesn't support this, so emulate it.
                return new Lazy<T>(() =>
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

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                throw new NotSupportedException("Not supported by System.Composition.");
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string? contractName)
            {
                throw new NotSupportedException("Not supported by System.Composition.");
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

            public T GetExportedValue<T>(string? contractName)
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

            public IEnumerable<Lazy<T>> GetExports<T>()
            {
                try
                {
                    return this.container.GetExports<T>().Select(v => new Lazy<T>(() => v));
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<T>> GetExports<T>(string? contractName)
            {
                try
                {
                    return this.container.GetExports<T>(contractName).Select(v => new Lazy<T>(() => v));
                }
                catch (System.Composition.Hosting.CompositionFailedException ex)
                {
                    throw new CompositionFailedException(ex.Message, ex);
                }
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                throw new NotSupportedException();
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string? contractName)
            {
                throw new NotSupportedException();
            }

            public IEnumerable<Lazy<object?, object>> GetExports(Type type, Type metadataViewType, string? contractName) => throw new NotSupportedException();

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

            public IEnumerable<T> GetExportedValues<T>(string? contractName)
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

            public IEnumerable<object> GetExportedValues(Type type, string? contractName) => this.container.GetExports(type, contractName);

            void IContainer.ReleaseExport<T>(Lazy<T> export) => throw new NotSupportedException();

            void IContainer.ReleaseExports<T>(IEnumerable<Lazy<T>> export) => throw new NotSupportedException();

            void IContainer.ReleaseExports<T, TMetadataView>(IEnumerable<Lazy<T, TMetadataView>> export) => throw new NotSupportedException();

            public void Dispose()
            {
                this.container.Dispose();
            }
        }

        internal class V3ContainerWrapper : IContainer
        {
            private readonly ExportProvider container;

            internal V3ContainerWrapper(ExportProvider container, CompositionConfiguration configuration)
            {
                Requires.NotNull(container, nameof(container));
                Requires.NotNull(configuration, nameof(configuration));

                this.container = container;
                this.Configuration = configuration;
            }

            internal ExportProvider ExportProvider
            {
                get { return this.container; }
            }

            internal CompositionConfiguration Configuration { get; private set; }

            public Lazy<T> GetExport<T>()
            {
                return this.container.GetExport<T>();
            }

            public Lazy<T> GetExport<T>(string? contractName)
            {
                return this.container.GetExport<T>(contractName);
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                return this.container.GetExport<T, TMetadataView>();
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string? contractName)
            {
                return this.container.GetExport<T, TMetadataView>(contractName);
            }

            public T GetExportedValue<T>()
            {
                return this.container.GetExportedValue<T>();
            }

            public T GetExportedValue<T>(string? contractName)
            {
                return this.container.GetExportedValue<T>(contractName);
            }

            public IEnumerable<Lazy<T>> GetExports<T>()
            {
                return this.container.GetExports<T>();
            }

            public IEnumerable<Lazy<T>> GetExports<T>(string? contractName)
            {
                return this.container.GetExports<T>(contractName);
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                return this.container.GetExports<T, TMetadataView>();
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string? contractName)
            {
                return this.container.GetExports<T, TMetadataView>(contractName);
            }

            public IEnumerable<Lazy<object?, object>> GetExports(Type type, Type metadataViewType, string? contractName) => this.container.GetExports(type, metadataViewType, contractName);

            public IEnumerable<T> GetExportedValues<T>()
            {
                return this.container.GetExportedValues<T>();
            }

            public IEnumerable<T> GetExportedValues<T>(string? contractName)
            {
                return this.container.GetExportedValues<T>(contractName);
            }

            public IEnumerable<object?> GetExportedValues(Type type, string? contractName) => this.container.GetExportedValues(type, contractName);

            public void ReleaseExport<T>(Lazy<T> export) => this.container.ReleaseExport(export);

            public void ReleaseExports<T>(IEnumerable<Lazy<T>> export) => this.container.ReleaseExports(export);

            public void ReleaseExports<T, TMetadataView>(IEnumerable<Lazy<T, TMetadataView>> export) => this.container.ReleaseExports(export);

            public void Dispose()
            {
                this.container.Dispose();
            }
        }
    }
}
