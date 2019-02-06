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
            var exports = composition.GetExports(typeof(IDisposable).ToString());
            Assert.Equal(0, exports.Count);
        }
    }
}
