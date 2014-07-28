namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition.Hosting;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;
    using MefV1 = System.ComponentModel.Composition;

    internal static class TestUtilities
    {
        internal static ExportProvider CreateContainer(this CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");

#if Runtime
            return configuration.CreateExportProviderFactory().CreateExportProvider();
#else
            string basePath = Path.GetTempFileName();
            string assemblyPath = basePath + ".dll";
            var compiledCacheManager = new CompiledComposition
            {
                AssemblyName = Path.GetFileNameWithoutExtension(assemblyPath),
            };

            if (Debugger.IsAttached)
            {
                compiledCacheManager.Optimize = false;
                using (var pdb = File.Open(basePath + ".pdb", FileMode.Create))
                {
                    using (var source = File.Open(basePath + ".cs", FileMode.Create))
                    {
                        compiledCacheManager.Optimize = false;
                        compiledCacheManager.PdbSymbols = pdb;
                        compiledCacheManager.Source = source;
                        using (var assemblyStream = File.Open(assemblyPath, FileMode.CreateNew))
                        {
                            compiledCacheManager.SaveAsync(configuration, assemblyStream).GetAwaiter().GetResult();
                        }
                    }
                }

                var exportProviderFactory = CompiledComposition.LoadExportProviderFactory(assemblyPath);
                return exportProviderFactory.CreateExportProvider();
            }
            else
            {
                Stream sourceFileStream = null;
#if DEBUG
                sourceFileStream = new MemoryStream();
#endif
                try
                {
                    compiledCacheManager.Source = sourceFileStream;
                    var assemblyStream = new MemoryStream();
                    compiledCacheManager.SaveAsync(configuration, assemblyStream).Wait();
                    assemblyStream.Position = 0;
                    var exportProvider = compiledCacheManager.LoadExportProviderFactoryAsync(assemblyStream).Result.CreateExportProvider();
                    return exportProvider;
                }
                finally
                {
                    if (sourceFileStream != null)
                    {
                        bool includeLineNumbers;
                        TextWriter sourceFileWriter;
                        if (sourceFileStream.Length < 200 * 1024) // the test results window doesn't do well with large output
                        {
                            includeLineNumbers = true;
                            sourceFileWriter = Console.Out;
                        }
                        else
                        {
                            // Write to a file instead and then emit its path to the output window.
                            string sourceFileName = Path.GetTempFileName() + ".cs";
                            sourceFileWriter = new StreamWriter(File.OpenWrite(sourceFileName));
                            Console.WriteLine("Source file written to: {0}", sourceFileName);
                            includeLineNumbers = false;
                        }

                        sourceFileStream.Position = 0;
                        var sourceFileReader = new StreamReader(sourceFileStream);
                        int lineNumber = 0;
                        string line;
                        while ((line = sourceFileReader.ReadLine()) != null)
                        {
                            if (includeLineNumbers)
                            {
                                sourceFileWriter.Write("Line {0,5}: ", ++lineNumber);
                            }

                            sourceFileWriter.WriteLine(line);
                        }

                        sourceFileWriter.Flush();
                        if (sourceFileWriter != Console.Out)
                        {
                            sourceFileWriter.Close();
                        }
                    }
                }
            }
#endif
        }

        internal static ExportProvider CreateContainer(params Type[] parts)
        {
            return CreateContainerAsync(parts).GetAwaiter().GetResult();
        }

        internal static async Task<ExportProvider> CreateContainerAsync(params Type[] parts)
        {
            return CompositionConfiguration.Create(
                await new AttributedPartDiscovery().CreatePartsAsync(parts))
                .CreateContainer();
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
            var assemblyParts = discovery.CreatePartsAsync(assemblies).GetAwaiter().GetResult();
            var catalog = ComposableCatalog.Create(assemblyParts);
            if (parts != null && parts.Length != 0)
            {
                var typeCatalog = ComposableCatalog.Create(discovery.CreatePartsAsync(parts).GetAwaiter().GetResult());
                catalog = ComposableCatalog.Create(catalog.Parts.Concat(typeCatalog.Parts));
            }

            return CreateContainerV3(catalog, attributesDiscovery, assemblies.ToImmutableHashSet());
        }

        private static PartDiscovery GetDiscoveryService(CompositionEngines attributesDiscovery)
        {
            var discovery = new List<PartDiscovery>(2);
            if (attributesDiscovery.HasFlag(CompositionEngines.V1))
            {
                discovery.Add(new AttributedPartDiscoveryV1());
            }

            if (attributesDiscovery.HasFlag(CompositionEngines.V2))
            {
                var v2Discovery = new AttributedPartDiscovery();
                if (attributesDiscovery.HasFlag(CompositionEngines.V3NonPublicSupport))
                {
                    v2Discovery.IsNonPublicSupported = true;
                }

                discovery.Add(v2Discovery);
            }

            return PartDiscovery.Combine(discovery.ToArray());
        }

        private static IContainer CreateContainerV3(ComposableCatalog catalog, CompositionEngines options, ImmutableHashSet<Assembly> additionalAssemblies = null)
        {
            var catalogWithCompositionService = catalog
                .WithCompositionService()
                .WithDesktopSupport();
            var configuration = CompositionConfiguration.Create(catalogWithCompositionService)
                .WithReferenceAssemblies(additionalAssemblies ?? ImmutableHashSet<Assembly>.Empty);
            if (!options.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors))
            {
                configuration.ThrowOnErrors();
            }

#if DGML
            string dgmlFile = System.IO.Path.GetTempFileName() + ".dgml";
            configuration.CreateDgml().Save(dgmlFile);
            Console.WriteLine("DGML saved to: " + dgmlFile);
#endif
            var container = configuration.CreateContainer();
            return new V3ContainerWrapper(container, configuration);
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

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV1AndV2AtOnce))
            {
                test(CreateContainerV3(parts, CompositionEngines.V1 | CompositionEngines.V2));
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
                test(CreateContainerV3(assemblies, CompositionEngines.V1 | (CompositionEngines.V3OptionsMask & attributesVersion), parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V2))
            {
                test(CreateContainerV2(assemblies, parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV2))
            {
                test(CreateContainerV3(assemblies, CompositionEngines.V2 | (CompositionEngines.V3OptionsMask & attributesVersion), parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V3EmulatingV1AndV2AtOnce))
            {
                test(CreateContainerV3(assemblies, CompositionEngines.V1 | CompositionEngines.V2 | (CompositionEngines.V3OptionsMask & attributesVersion), parts));
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
            }

            public Lazy<T> GetExport<T>(string contractName)
            {
                try
                {
                    return this.container.GetExport<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
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
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
            {
                try
                {
                    return this.container.GetExport<T, TMetadataView>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
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
            }

            public IEnumerable<Lazy<T>> GetExports<T>(string contractName)
            {
                try
                {
                    return this.container.GetExports<T>(contractName);
                }
                catch (MefV1.ImportCardinalityMismatchException ex)
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
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
            {
                try
                {
                    return this.container.GetExports<T, TMetadataView>(contractName);
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

            public Lazy<T> GetExport<T>()
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

            public Lazy<T> GetExport<T>(string contractName)
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

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                throw new NotSupportedException("Not supported by System.Composition.");
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
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

            public IEnumerable<Lazy<T>> GetExports<T>(string contractName)
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

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
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

        internal class V3ContainerWrapper : IContainer
        {
            private readonly ExportProvider container;

            internal V3ContainerWrapper(ExportProvider container, CompositionConfiguration configuration)
            {
                Requires.NotNull(container, "container");
                Requires.NotNull(configuration, "configuration");

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

            public Lazy<T> GetExport<T>(string contractName)
            {
                return this.container.GetExport<T>(contractName);
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>()
            {
                return this.container.GetExport<T, TMetadataView>();
            }

            public Lazy<T, TMetadataView> GetExport<T, TMetadataView>(string contractName)
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

            public IEnumerable<Lazy<T>> GetExports<T>()
            {
                return this.container.GetExports<T>();
            }

            public IEnumerable<Lazy<T>> GetExports<T>(string contractName)
            {
                return this.container.GetExports<T>(contractName);
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>()
            {
                return this.container.GetExports<T, TMetadataView>();
            }

            public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
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
    }
}
