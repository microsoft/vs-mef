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
        public async Task CacheAndReload()
        {
            var configuration = CompositionConfiguration.Create(typeof(SomeExport));
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            await configuration.SaveAsync(path);
            configuration = null;

            var reconstitutedConfiguration = CompositionConfiguration.Load(path);
            var container = reconstitutedConfiguration.CreateContainer();
            SomeExport export = container.GetExportedValue<SomeExport>();
            Assert.NotNull(export);
        }

        [Export]
        public class SomeExport { }
    }
}
