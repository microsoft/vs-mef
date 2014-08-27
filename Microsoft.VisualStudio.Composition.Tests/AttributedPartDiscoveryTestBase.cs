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
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Microsoft.VisualStudio.Composition.BrokenAssemblyTests;
    using Xunit;
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
        public void TypeDiscoveryIgnoresPartNotDiscoverableAttribute()
        {
            var result = this.DiscoveryService.CreatePart(typeof(NonDiscoverablePart));
            Assert.NotNull(result);
        }

        [Fact]
        public async Task AssemblyDiscoveryOmitsNonDiscoverableParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonPart))));
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePart))));
        }

        [Fact]
        public async Task AssemblyDiscoveryOmitsNonDiscoverableParts_Combined()
        {
            var combined = PartDiscovery.Combine(this.DiscoveryService, new PartDiscoveryAllTypesMock());
            var result = await combined.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);

            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonPart))));
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePart))));
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePartV1))));
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePartV2))));

            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart1))));
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart2))));
        }

        [Fact]
        public async Task AssemblyDiscoveryFindsNestedParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(OuterClass.NestedPart))));
        }

        [SkippableFact]
        public async Task AssemblyDiscoveryDropsTypesWithProblematicAttributes()
        {
            // If this assert fails, it means that the assembly that is supposed to be undiscoverable
            // by this unit test is actually discoverable. Check that CopyLocal=false for all references
            // to Microsoft.VisualStudio.Composition.MissingAssemblyTests and that the assembly
            // is not building to the same directory as the test assemblies.
            try
            {
                typeof(TypeWithMissingAttribute).GetCustomAttributes(false);
                throw new SkippableFactAttribute.SkipException("The missing assembly is present. Test cannot verify proper operation.");
            }
            catch (FileNotFoundException) { }

            var result = await this.DiscoveryService.CreatePartsAsync(typeof(TypeWithMissingAttribute).Assembly);

            // Verify that we still found parts.
            Assert.NotEqual(0, result.Parts.Count);
        }

        [SkippableFact]
        public async Task AssemblyDiscoveryDropsAssembliesWithProblematicTypes()
        {
            // If this assert fails, it means that the assembly that is supposed to be undiscoverable
            // by this unit test is actually discoverable. Check that CopyLocal=false for all references
            // to Microsoft.VisualStudio.Composition.MissingAssemblyTests and that the assembly
            // is not building to the same directory as the test assemblies.
            try
            {
                typeof(TypeWithMissingAttribute).GetCustomAttributes(false);
                throw new SkippableFactAttribute.SkipException("The missing assembly is present. Test cannot verify proper operation.");
            }
            catch (FileNotFoundException) { }

            var result = await this.DiscoveryService.CreatePartsAsync(
                new List<Assembly>{
                    typeof(TypeWithMissingAttribute).Assembly,
                    typeof(GoodType).Assembly });

            // Verify that we still found parts.
            Assert.NotEqual(0, result.Parts.Count);
        }

        #region TypeDiscoveryOmitsNestedTypes test

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(OuterClass))]
        public void TypeDiscoveryOmitsNestedTypes(IContainer container)
        {
            Assert.Equal(0, container.GetExportedValues<OuterClass.NestedPart>().Count());
        }

        #endregion

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

        /// <summary>
        /// A discovery mock that produces no parts, but includes all types for consideration.
        /// </summary>
        private class PartDiscoveryAllTypesMock : PartDiscovery
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
                return assembly.GetTypes();
            }
        }
    }
}
