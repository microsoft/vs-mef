namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
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

        [Export]
        public class SomeExport { }
    }
}
