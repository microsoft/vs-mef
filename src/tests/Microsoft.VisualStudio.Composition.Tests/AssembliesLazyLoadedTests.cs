// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    [Trait("Efficiency", "LazyLoad")]
    [Trait("AppDomains", "true")]
    public abstract class AssembliesLazyLoadedTests : IDisposable
    {
        private ICompositionCacheManager cacheManager;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private TempFileCollection tfc;
#pragma warning restore CA2213 // Disposable fields should be disposed

        protected AssembliesLazyLoadedTests(ICompositionCacheManager cacheManager)
        {
            Requires.NotNull(cacheManager, nameof(cacheManager));

            this.cacheManager = cacheManager;
            this.tfc = new TempFileCollection();
        }

        public void Dispose()
        {
            this.tfc.Delete();
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesLazyLoadedWhenQueried()
        {
            SkipOnMono();

            var exportedTypes = new List<Type>
            {
                typeof(ExternalExport),
                typeof(YetAnotherExport),
                typeof(ExternalExportOnMember),
            };

            var catalog = TestUtilities.EmptyCatalog.AddParts(await TestUtilities.V2Discovery.CreatePartsAsync(exportedTypes));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            foreach (var exportedType in exportedTypes)
            {
                catalogCache.Position = 0;
                compositionCache.Position = 0;

                // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
                var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
                try
                {
                    var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                    driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);
                    driver.TestExternalExport(exportedType.Assembly.Location, exportedType.FullName);
                }
                finally
                {
                    AppDomain.Unload(appDomain);
                }
            }
        }

        [Fact]
        public async Task CatalogGetInputAssembliesDoesNotLoadLazyExports()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(ExternalExportWithExternalMetadataType), typeof(ExternalExportWithExternalMetadataTypeArray), typeof(ExternalExportWithExternalMetadataEnum32)));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);

                // GetInputAssemblies should not load the YetAnotherExport assembly or the CustomEnum assembly (both in AppDomainTests2)
                driver.TestGetInputAssembliesDoesNotLoadLazyExport(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesLazyLoadedByLazyImport()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(ExternalExportWithLazy), typeof(YetAnotherExport)));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);
                driver.TestExternalExportWithLazy(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesAssignabilityChecks()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(PartThatLazyImportsExportWithMetadataOfCustomType), typeof(ExportWithCustomMetadata)));
            var catalogCache = await this.SaveCatalogAsync(catalog);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Compose(catalogCache);
                driver.AssertAssembliesNotLoaded(typeof(PartThatLazyImportsExportWithMetadataOfCustomType).Assembly.Location, typeof(ExportWithCustomMetadata).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesMemberExportAssignabilityChecks()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(PartThatImportsExportedMember), typeof(ExportedMember)));
            var catalogCache = await this.SaveCatalogAsync(catalog);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Compose(catalogCache);
                driver.AssertAssembliesNotLoaded(typeof(PartThatImportsExportedMember).Assembly.Location, typeof(ExportedMember).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when
        /// their metadata is actually retrieved.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesLazyLoadedByLazyMetadataDictionary()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(PartThatLazyImportsExportWithTypeMetadataViaDictionary), typeof(AnExportWithMetadataTypeValue)));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);
                driver.TestPartThatImportsExportWithTypeMetadataViaDictionary(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when
        /// their metadata is actually retrieved.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesLazyLoadedByLazyTMetadata()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(PartThatLazyImportsExportWithTypeMetadataViaTMetadata), typeof(AnExportWithMetadataTypeValue)));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);
                driver.TestPartThatImportsExportWithTypeMetadataViaTMetadata(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that lazy assembly load isn't defeated when that assembly
        /// defines a type used as a generic type argument elsewhere.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesLazyLoadedWithGenericTypeArg()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(PartImportingOpenGenericExport), typeof(OpenGenericExport<>)));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);
                driver.TestPartThatImportsExportWithGenericTypeArg(typeof(SomeOtherType).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when the custom metadata types they define
        /// are actually required by some import.
        /// </summary>
        [SkippableFact]
        public async Task ComposableAssembliesLazyLoadedWhenCustomMetadataIsRequired()
        {
            SkipOnMono();
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(ExportWithCustomMetadata), typeof(PartThatLazyImportsExportWithMetadataOfCustomType)));
            var catalogCache = await this.SaveCatalogAsync(catalog);
            var configuration = CompositionConfiguration.Create(catalog);
            var compositionCache = await this.SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(this.cacheManager.GetType(), compositionCache, catalogCache);
                driver.TestPartThatLazyImportsExportWithMetadataOfCustomType(typeof(CustomEnum).Assembly.Location, this is AssembliesLazyLoadedDataFileCacheTests);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        private static void SkipOnMono()
        {
            TestUtilities.SkipOnMono("Assemblies are loaded more eagerly in other AppDomains on Mono");
        }

        private async Task<Stream> SaveConfigurationAsync(CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, nameof(configuration));

            var ms = new MemoryStream();
            await this.cacheManager.SaveAsync(configuration, ms);
            ms.Position = 0;
            return ms;
        }

        private async Task<Stream> SaveCatalogAsync(ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));

            var ms = new MemoryStream();
            await new CachedCatalog().SaveAsync(catalog, ms);
            ms.Position = 0;
            return ms;
        }

        private class AppDomainTestDriver : MarshalByRefObject
        {
            private ExportProvider? container;
            private ComposableCatalog? catalog;

            internal void Initialize(Type cacheManagerType, Stream cachedComposition, Stream cachedCatalog)
            {
                Requires.NotNull(cacheManagerType, nameof(cacheManagerType));
                Requires.NotNull(cachedComposition, nameof(cachedComposition));
                Requires.NotNull(cachedCatalog, nameof(cachedCatalog));

                // Copy the streams to ones inside our app domain.
                Stream cachedCompositionLocal = CopyStream(cachedComposition);
                Stream cachedCatalogLocal = CopyStream(cachedCatalog);

                // Deserialize the catalog to verify that it doesn't load any assemblies.
                var catalogManager = new CachedCatalog();
                this.catalog = catalogManager.LoadAsync(cachedCatalogLocal, TestUtilities.Resolver).Result;

                // Deserialize the composition to prepare for the rest of the test.
                var cacheManager = (ICompositionCacheManager)Activator.CreateInstance(cacheManagerType);
                var containerFactory = cacheManager.LoadExportProviderFactoryAsync(cachedCompositionLocal, TestUtilities.Resolver).GetAwaiter().GetResult();
                this.container = containerFactory.CreateExportProvider();
            }

            internal void Compose(Stream cachedCatalog)
            {
                Requires.NotNull(cachedCatalog, nameof(cachedCatalog));

                Stream cachedCatalogLocal = CopyStream(cachedCatalog);

                // Deserialize the catalog to verify that it doesn't load any assemblies.
                var catalogManager = new CachedCatalog();
                this.catalog = catalogManager.LoadAsync(cachedCatalogLocal, TestUtilities.Resolver).Result;

                var configuration = CompositionConfiguration.Create(this.catalog);

                var cacheManager = new CachedComposition();
                var ms = new MemoryStream();
                cacheManager.SaveAsync(configuration, ms).GetAwaiter().GetResult();
                ms.Position = 0;

                var containerFactory = cacheManager.LoadExportProviderFactoryAsync(ms, TestUtilities.Resolver).GetAwaiter().GetResult();
                this.container = containerFactory.CreateExportProvider();
            }

            internal void TestGetInputAssembliesDoesNotLoadLazyExport(string lazyLoadedAssemblyPath)
            {
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                this.catalog!.GetInputAssemblies();
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestExternalExport(string lazyLoadedAssemblyPath, string contractName)
            {
                // Verify that before the test, we haven't loaded the assembly.
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));

                // Now query for the export, but don't evaluate it yet. This shouldn't load the assembly.
                var export = this.container!.GetExport<object>(contractName);
                AssertEx.NotNull(export);
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));

                // Now evaluate it, and confirm that it loads the assembly (to verify the validity of the test).
                object value = export.Value;
                AssertEx.NotNull(value);
                AssertEx.True(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestExternalExportWithLazy(string lazyLoadedAssemblyPath)
            {
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = this.container!.GetExportedValue<ExternalExportWithLazy>();
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.NotNull(exportWithLazy.YetAnotherExport.Value);
                AssertEx.True(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void AssertAssembliesNotLoaded(params string[] assemblyPaths)
            {
                AssertEx.False(GetLoadedAssemblies().Select(a => a.Location).Intersect(assemblyPaths, StringComparer.OrdinalIgnoreCase).Any());
            }

            internal void TestPartThatImportsExportWithTypeMetadataViaDictionary(string lazyLoadedAssemblyPath)
            {
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = this.container!.GetExportedValue<PartThatLazyImportsExportWithTypeMetadataViaDictionary>();
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.NotNull(exportWithLazy.ImportWithDictionary.Metadata.ContainsKey("foo"));
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Type type = (Type)exportWithLazy.ImportWithDictionary.Metadata["SomeType"];
                Type[] types = (Type[])exportWithLazy.ImportWithDictionary.Metadata["SomeTypes"];
                AssertEx.Equal("YetAnotherExport", type.Name);
                types.Single(t => t.Name == "String");
                types.Single(t => t.Name == "YetAnotherExport");
                AssertEx.True(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestPartThatImportsExportWithTypeMetadataViaTMetadata(string lazyLoadedAssemblyPath)
            {
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = this.container!.GetExportedValue<PartThatLazyImportsExportWithTypeMetadataViaTMetadata>();
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.Equal("default", exportWithLazy.ImportWithTMetadata.Metadata.SomeProperty);
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Type type = exportWithLazy.ImportWithTMetadata.Metadata.SomeType;
                Type[] types = exportWithLazy.ImportWithTMetadata.Metadata.SomeTypes;
                AssertEx.Equal("YetAnotherExport", type.Name);
                types.Single(t => t.Name == "String");
                types.Single(t => t.Name == "YetAnotherExport");
                AssertEx.True(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestPartThatImportsExportWithGenericTypeArg(string lazyLoadedAssemblyPath)
            {
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = this.container!.GetExportedValue<PartImportingOpenGenericExport>();
                AssertEx.True(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestPartThatLazyImportsExportWithMetadataOfCustomType(string lazyLoadedAssemblyPath, bool isRuntime)
            {
                AssertEx.False(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = this.container!.GetExportedValue<PartThatLazyImportsExportWithMetadataOfCustomType>();

                // This next segment we'll permit an assembly load only for code gen cases, which aren't as well tuned at present.
                AssertEx.False(isRuntime && GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.Equal("Value", exportWithLazy.ImportingProperty.Metadata["Simple"] as string);
                AssertEx.False(isRuntime && GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));

                // At this point, loading the assembly is absolutely required.
                object? customEnum = exportWithLazy.ImportingProperty.Metadata["CustomValue"];
                Assert.Equal("CustomEnum", customEnum?.GetType().Name);
                AssertEx.True(GetLoadedAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            private static IEnumerable<Assembly> GetLoadedAssemblies()
            {
                return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
            }

            private static Stream CopyStream(Stream source)
            {
                Stream copy = new MemoryStream();
                source.CopyTo(copy);
                copy.Position = 0;
                return copy;
            }
        }
    }
}

#endif
