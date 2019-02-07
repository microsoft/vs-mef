namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using Xunit;

    public class RuntimeCompositionTests
    {
        [Fact]
        public void TestEmptyCatalogTest()
        {
            var configuration = CompositionConfiguration.Create(TestUtilities.EmptyCatalog);
            var composition = RuntimeComposition.CreateRuntimeComposition(configuration);
            var factory = composition.CreateExportProviderFactory();
            var provider = factory.CreateExportProvider();
            var exports = provider.GetExports<IDisposable>();
            Assert.Empty(exports);
        }
    }
}
