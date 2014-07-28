namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;
    using Xunit;

    [Trait("Efficiency", "LazyLoad")]
    public class AssembliesLazyLoadedTests
    {
        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [Fact]
        public async Task ComposableAssembliesLazyLoadedWhenQueried()
        {
            var configuration = CompositionConfiguration.Create(await new AttributedPartDiscovery().CreatePartsAsync(typeof(ExternalExport), typeof(YetAnotherExport)));
            string dllPath = await SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(dllPath);
                driver.TestExternalExport(typeof(ExternalExport).Assembly.Location);
                driver.TestYetAnotherExport(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [Fact]
        public async Task ComposableAssembliesLazyLoadedByLazyImport()
        {
            var configuration = CompositionConfiguration.Create(
                await new AttributedPartDiscovery().CreatePartsAsync(typeof(ExternalExportWithLazy), typeof(YetAnotherExport)));
            string dllPath = await SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(dllPath);
                driver.TestExternalExportWithLazy(typeof(YetAnotherExport).Assembly.Location);
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
        [Fact]
        public async Task ComposableAssembliesLazyLoadedByLazyMetadataDictionary()
        {
            var configuration = CompositionConfiguration.Create(
                await new AttributedPartDiscovery().CreatePartsAsync(typeof(PartThatLazyImportsExportWithTypeMetadataViaDictionary), typeof(AnExportWithMetadataTypeValue)));
            string dllPath = await SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(dllPath);
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
        [Fact]
        public async Task ComposableAssembliesLazyLoadedByLazyTMetadata()
        {
            var configuration = CompositionConfiguration.Create(
                await new AttributedPartDiscovery().CreatePartsAsync(typeof(PartThatLazyImportsExportWithTypeMetadataViaTMetadata), typeof(AnExportWithMetadataTypeValue)));
            string dllPath = await SaveConfigurationAsync(configuration);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(dllPath);
                driver.TestPartThatImportsExportWithTypeMetadataViaTMetadata(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        private static async Task<string> SaveConfigurationAsync(CompositionConfiguration configuration)
        {
            string rootpath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string dllPath = rootpath + ".dll";
            string pdbPath = rootpath + ".pdb";
            string csPath = rootpath + ".cs";
            await configuration.CompileAsync(dllPath, pdbPath, csPath, debug: true);
            return dllPath;
        }

        private class AppDomainTestDriver : MarshalByRefObject
        {
            private ExportProvider container;

            internal void Initialize(string cachedCompositionPath)
            {
                var containerFactory = CompiledComposition.Load(Assembly.LoadFile(cachedCompositionPath));
                this.container = containerFactory.CreateExportProvider();
            }

            internal void TestExternalExport(string lazyLoadedAssemblyPath)
            {
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                this.CauseLazyLoad1(container);
                Assert.True(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestYetAnotherExport(string lazyLoadedAssemblyPath)
            {
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                this.CauseLazyLoad2(container);
                Assert.True(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestExternalExportWithLazy(string lazyLoadedAssemblyPath)
            {
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = container.GetExportedValue<ExternalExportWithLazy>();
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.NotNull(exportWithLazy.YetAnotherExport.Value);
                Assert.True(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestPartThatImportsExportWithTypeMetadataViaDictionary(string lazyLoadedAssemblyPath)
            {
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = container.GetExportedValue<PartThatLazyImportsExportWithTypeMetadataViaDictionary>();
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.NotNull(exportWithLazy.ImportWithDictionary.Metadata.ContainsKey("foo"));
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Type type = (Type)exportWithLazy.ImportWithDictionary.Metadata["SomeType"];
                Type[] types = (Type[])exportWithLazy.ImportWithDictionary.Metadata["SomeTypes"];
                Assert.Equal("YetAnotherExport", type.Name);
                types.Single(t => t.Name == "String");
                types.Single(t => t.Name == "YetAnotherExport");
                Assert.True(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            internal void TestPartThatImportsExportWithTypeMetadataViaTMetadata(string lazyLoadedAssemblyPath)
            {
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                var exportWithLazy = container.GetExportedValue<PartThatLazyImportsExportWithTypeMetadataViaTMetadata>();
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.Equal("default", exportWithLazy.ImportWithTMetadata.Metadata.SomeProperty);
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Type type = exportWithLazy.ImportWithTMetadata.Metadata.SomeType;
                Type[] types = exportWithLazy.ImportWithTMetadata.Metadata.SomeTypes;
                Assert.Equal("YetAnotherExport", type.Name);
                types.Single(t => t.Name == "String");
                types.Single(t => t.Name == "YetAnotherExport");
                Assert.True(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
            }

            [MethodImpl(MethodImplOptions.NoInlining)] // if this method is inlined, it defeats the point of it being a separate method in the test and causes test failure.
            private void CauseLazyLoad1(ExportProvider container)
            {
                // Actually the lazy load happens before GetExport is actually called since this method
                // references a type in that assembly.
                var export = container.GetExportedValue<ExternalExport>();
                Assert.NotNull(export);
            }

            [MethodImpl(MethodImplOptions.NoInlining)] // if this method is inlined, it defeats the point of it being a separate method in the test and causes test failure.
            private void CauseLazyLoad2(ExportProvider container)
            {
                // Actually the lazy load happens before GetExport is actually called since this method
                // references a type in that assembly.
                var export = container.GetExportedValue<YetAnotherExport>();
                Assert.NotNull(export);
            }
        }
    }
}
