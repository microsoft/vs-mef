namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
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
            Assert.Equal(1, result.ExportedTypes.Count);
            Assert.Equal(0, result.ImportingMembers.Count);
            Assert.False(result.IsShared);
        }

        [Fact]
        public void SharedPartProduction()
        {
            ComposablePartDefinition result = this.DiscoveryService.CreatePart(typeof(SharedPart));
            Assert.NotNull(result);
            Assert.Equal(1, result.ExportedTypes.Count);
            Assert.Equal(0, result.ImportingMembers.Count);
            Assert.True(result.IsShared);
        }

        [Fact]
        public async Task AssemblyDiscoveryFindsTopLevelParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart1))));
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart2))));
        }

        [Fact]
        public async Task AssemblyDiscoveryOmitsNonDiscoverableParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonPart))));
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePart))));
        }

        [Fact]
        public async Task AssemblyDiscoveryFindsNestedParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(OuterClass.NestedPart))));
        }

        [Fact]
        public async Task AssemblyGetTypesError()
        {
            var assembly = new SketchyAssembly();
            var result = await this.DiscoveryService.CreatePartsAsync(assembly);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPart { }

        [Export, Shared]
        [MefV1.Export]
        public class SharedPart { }

        #region Indexer overloading tests

        [Fact]
        public void IndexerInDerivedAndBase()
        {
            var part = this.DiscoveryService.CreatePart(typeof(DerivedTypeWithIndexer));
        }

        public class BaseTypeWithIndexer
        {
            public virtual string this[int index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public virtual string this[string index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }
        }

        [Export]
        public class DerivedTypeWithIndexer : BaseTypeWithIndexer
        {
            public override string this[int index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }
        }

        #endregion

        
        [AttributeUsage(AttributeTargets.All)]
        private class SketchyAttribute : Attribute
        {
            public SketchyAttribute() { throw new ArgumentException(); }

            public Attribute GetCustomAttribute(MemberInfo info, Type attributeType)
            {
                throw new ArgumentException();
            }
        }

        [Sketchy]
        private class SketchyType
        {
        }

        private class SketchyAssembly : Assembly
        {
            public override System.Type[] GetTypes()
            {
                return new Type[] { typeof(SketchyType) };
            }
        }
    }
}
