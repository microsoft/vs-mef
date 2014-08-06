namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class PartDiscoveryTests
    {
        [Fact]
        public async Task CreatePartsAsync_TypeArray_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(typeof(string), typeof(int));
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(1, parts.Parts.Count);
        }

        [Fact]
        public async Task CreatePartsAsync_Assembly_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(this.GetType().Assembly);
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(0, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_CreatePartsAsync_TypeArray_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(typeof(string), typeof(int));
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(1, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_CreatePartsAsync_Assembly_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(this.GetType().Assembly);
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(0, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_CreatePartsAsync_AssemblyEnumerable_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(new[] { this.GetType().Assembly });
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(0, parts.Parts.Count);
        }

        private class SketchyPartDiscovery : PartDiscovery
        {
            protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
            {
                if (partType == typeof(string))
                {
                    throw new ArgumentException();
                }

                return new ComposablePartDefinition(
                    TypeRef.Get(typeof(int)),
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberRef, IReadOnlyCollection<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
                    default(MethodRef),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    CreationPolicy.Any,
                    true);
            }

            public override bool IsExportFactoryType(Type type)
            {
                return false;
            }

            protected override IEnumerable<Type> GetTypes(System.Reflection.Assembly assembly)
            {
                throw new ArgumentException();
            }
        }

        private class NoOpDiscovery : PartDiscovery
        {
            protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
            {
                return null;
            }

            public override bool IsExportFactoryType(Type type)
            {
                return false;
            }

            protected override IEnumerable<Type> GetTypes(Assembly assembly)
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
