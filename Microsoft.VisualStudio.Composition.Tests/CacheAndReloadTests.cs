namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;
    using Xunit;

    public class CacheAndReloadTests
    {
        [Fact]
        public void CacheAndReload()
        {
            var configuration = CompositionConfiguration.Create(typeof(SomeExport));
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            configuration.Save(path);
            configuration = null;

            var reconstitutedConfiguration = CompositionConfiguration.Load(path);
            var container = reconstitutedConfiguration.CreateContainer();
            SomeExport export = container.GetExport<SomeExport>();
            Assert.NotNull(export);
        }

        /// <summary>
        /// Verifies that the assemblies that MEF parts belong to are only loaded when their parts are actually instantiated.
        /// </summary>
        [Fact]
        public void ComposableAssembliesLazyLoadedWhenQueried()
        {
            var configuration = CompositionConfiguration.Create(typeof(ExternalExport), typeof(YetAnotherExport));
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            configuration.Save(path);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(path);
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
        public void ComposableAssembliesLazyLoadedByLazyImport()
        {
            var configuration = CompositionConfiguration.Create(typeof(ExternalExport), typeof(YetAnotherExport));
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            configuration.Save(path);

            // Use a sub-appdomain so we can monitor which assemblies get loaded by our composition engine.
            var appDomain = AppDomain.CreateDomain("Composition Test sub-domain", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainTestDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestDriver).Assembly.FullName, typeof(AppDomainTestDriver).FullName);
                driver.Initialize(path);
                driver.TestExternalExportWithLazy(typeof(YetAnotherExport).Assembly.Location);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        [Export]
        public class SomeExport { }

        private class AppDomainTestDriver : MarshalByRefObject
        {
            private CompositionContainer container;

            internal void Initialize(string cachedCompositionPath)
            {
                var containerFactory = CompositionConfiguration.Load(cachedCompositionPath);
                this.container = containerFactory.CreateContainer();
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
                var exportWithLazy = container.GetExport<ExternalExportWithLazy>();
                Assert.False(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));
                Assert.NotNull(exportWithLazy.YetAnotherExport.Value);
                Assert.True(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.Location.Equals(lazyLoadedAssemblyPath, StringComparison.OrdinalIgnoreCase)));

            }

            private void CauseLazyLoad1(CompositionContainer container)
            {
                // Actually the lazy load happens before GetExport is actually called since this method
                // references a type in that assembly.
                var export = container.GetExport<ExternalExport>();
                Assert.NotNull(export);
            }

            private void CauseLazyLoad2(CompositionContainer container)
            {
                // Actually the lazy load happens before GetExport is actually called since this method
                // references a type in that assembly.
                var export = container.GetExport<YetAnotherExport>();
                Assert.NotNull(export);
            }
        }
    }
}
