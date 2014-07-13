namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class PartDiscoveryTests
    {
        [Fact]
        public async Task CreatePartsAsync_TypeArray_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(typeof(string), typeof(int));
            Assert.Equal(1, parts.Count);
        }

        [Fact]
        public async Task CreatePartsAsync_Assembly_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(this.GetType().Assembly);
            Assert.Equal(0, parts.Count);
        }

        private class SketchyPartDiscovery : PartDiscovery
        {
            public override ComposablePartDefinition CreatePart(Type partType)
            {
                if (partType == typeof(string))
                {
                    throw new ArgumentException();
                }

                return new ComposablePartDefinition(
                    typeof(int),
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberInfo, IReadOnlyList<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    CreationPolicy.Any);
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
    }
}
