namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;
    using Xunit;

    public class CacheAndReloadTests
    {
        private ICompositionCacheManager cacheManager;

        public CacheAndReloadTests()
        {
            this.cacheManager = new CompiledComposition
            {
                AssemblyName = "CacheAndReloadTestCompilation",
            };
        }

        [Fact]
        public async Task CacheAndReload()
        {
            var configuration = CompositionConfiguration.Create(
                new[] { new AttributedPartDiscovery().CreatePart(typeof(SomeExport)) });
            var ms = new MemoryStream();
            await this.cacheManager.SaveAsync(configuration, ms);
            configuration = null;

            ms.Position = 0;
            var exportProviderFactory = await this.cacheManager.LoadExportProviderFactoryAsync(ms);
            var container = exportProviderFactory.CreateExportProvider();
            SomeExport export = container.GetExportedValue<SomeExport>();
            Assert.NotNull(export);
        }

        [Export]
        public class SomeExport { }
    }
}
