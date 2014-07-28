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
        [Fact]
        public async Task CacheAndReload()
        {
            var configuration = CompositionConfiguration.Create(
                new[] { new AttributedPartDiscovery().CreatePart(typeof(SomeExport)) });
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            await configuration.CompileAsync(path);
            configuration = null;

            var reconstitutedConfiguration = CompiledComposition.Load(Assembly.LoadFile(path));
            var container = reconstitutedConfiguration.CreateExportProvider();
            SomeExport export = container.GetExportedValue<SomeExport>();
            Assert.NotNull(export);
        }

        [Export]
        public class SomeExport { }
    }
}
