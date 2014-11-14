namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ComposableCatalogTests
    {
        [Fact]
        public async Task WithCatalog_MergesErrors()
        {
            var discovery = new AttributedPartDiscovery();
            var result1 = ComposableCatalog.Create(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist.dll" }));
            var result2 = ComposableCatalog.Create(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist2.dll" }));

            var mergedCatalog = result1.WithCatalog(result2);

            Assert.Equal(result1.DiscoveredParts.DiscoveryErrors.Count + result2.DiscoveredParts.DiscoveryErrors.Count, mergedCatalog.DiscoveredParts.DiscoveryErrors.Count);
            Assert.NotEqual(0, mergedCatalog.DiscoveredParts.DiscoveryErrors.Count); // the test is ineffective otherwise.
        }

        [Fact]
        public async Task WithCatalogs_MergesErrors()
        {
            var discovery = new AttributedPartDiscovery();
            var result1 = ComposableCatalog.Create(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist.dll" }));
            var result2 = ComposableCatalog.Create(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist2.dll" }));
            var result3 = ComposableCatalog.Create(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist3.dll" }));

            var mergedCatalog = result1.WithCatalogs(new[] { result2, result3 });

            Assert.Equal(
                result1.DiscoveredParts.DiscoveryErrors.Count + result2.DiscoveredParts.DiscoveryErrors.Count + result3.DiscoveredParts.DiscoveryErrors.Count,
                mergedCatalog.DiscoveredParts.DiscoveryErrors.Count);
            Assert.NotEqual(0, mergedCatalog.DiscoveredParts.DiscoveryErrors.Count); // the test is ineffective otherwise.
        }
    }
}
