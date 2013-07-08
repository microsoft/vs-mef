namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;
    using System.Composition;
    using MefV1 = System.ComponentModel.Composition;

    public abstract class AttributedPartDiscoveryTestBase
    {
        protected abstract PartDiscovery DiscoveryService { get; }

        [Fact]
        public void NonSharedPartProduction()
        {
            ComposablePartDefinition result = this.DiscoveryService.CreatePart(typeof(NonSharedPart));
            Assert.NotNull(result);
            Assert.Equal(1, result.ExportDefinitionsOnType.Count);
            Assert.Equal(0, result.ImportDefinitions.Count);
            Assert.False(result.IsShared);
        }

        [Fact]
        public void SharedPartProduction()
        {
            ComposablePartDefinition result = this.DiscoveryService.CreatePart(typeof(SharedPart));
            Assert.NotNull(result);
            Assert.Equal(1, result.ExportDefinitionsOnType.Count);
            Assert.Equal(0, result.ImportDefinitions.Count);
            Assert.True(result.IsShared);
        }

        [Fact(Skip = "Feature not yet implemented.")]
        public void AssemblyDiscoveryFindsTopLevelParts()
        {
            var parts = this.DiscoveryService.CreateParts(typeof(NonDiscoverablePart).Assembly);
            Assert.True(parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart1))));
            Assert.True(parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart2))));
        }

        [Fact(Skip = "Feature not yet implemented.")]
        public void AssemblyDiscoveryOmitsNonDiscoverableParts()
        {
            var parts = this.DiscoveryService.CreateParts(typeof(NonDiscoverablePart).Assembly);
            Assert.False(parts.Any(p => p.Type.IsEquivalentTo(typeof(NonPart))));
            Assert.False(parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePart))));
        }

        [Fact(Skip = "Feature not yet implemented.")]
        public void AssemblyDiscoveryFindsNestedParts()
        {
            var parts = this.DiscoveryService.CreateParts(typeof(NonDiscoverablePart).Assembly);
            Assert.True(parts.Any(p => p.Type.IsEquivalentTo(typeof(OuterClass.NestedPart))));
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPart { }

        [Export, Shared]
        [MefV1.Export]
        public class SharedPart { }
    }
}
