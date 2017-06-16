// Copyright (c) Microsoft. All rights reserved.

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
    using Composition.Reflection;
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
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).GetTypeInfo().Assembly);
            Assert.True(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(DiscoverablePart1))));
            Assert.True(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(DiscoverablePart2))));
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
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).GetTypeInfo().Assembly);
            Assert.False(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(NonPart))));
            Assert.False(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(NonDiscoverablePart))));
        }

        [Fact]
        public async Task AssemblyDiscoveryOmitsNonDiscoverableParts_Combined()
        {
            var combined = PartDiscovery.Combine(this.DiscoveryService, new PartDiscoveryAllTypesMock());
            var result = await combined.CreatePartsAsync(typeof(NonDiscoverablePart).GetTypeInfo().Assembly);

            Assert.False(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(NonPart))));
            Assert.False(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(NonDiscoverablePart))));
            Assert.False(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(NonDiscoverablePartV1))));
            Assert.False(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(NonDiscoverablePartV2))));

            Assert.True(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(DiscoverablePart1))));
            Assert.True(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(DiscoverablePart2))));
        }

        [Fact]
        public async Task AssemblyDiscoveryFindsNestedParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).GetTypeInfo().Assembly);
            Assert.True(result.Parts.Any(p => p.Type.GetTypeInfo().IsEquivalentTo(typeof(OuterClass.NestedPart))));
        }

        /// <summary>
        /// Verifies that assemblies are loaded into the Load context rather than the LoadFrom context.
        /// </summary>
        /// See also Choosing a Binding Context (http://blogs.msdn.com/b/suzcook/archive/2003/05/29/57143.aspx)
        [Fact]
        public async Task AssemblyLoadContext()
        {
            // The way this test works is we copy an assembly that we reference to another location.
            // Then we perform discovery at that other location explicitly.
            // If discovery is using the LoadFrom context, this will cause the assembly to be loaded twice
            // such that we won't be able to consume the result and GetExportedValue will throw.
            string alternateReadLocation = Path.GetTempFileName();
            File.Copy(typeof(DiscoverablePart1).GetTypeInfo().Assembly.Location, alternateReadLocation, true);

            var parts = await this.DiscoveryService.CreatePartsAsync(new[] { alternateReadLocation });
            var catalog = TestUtilities.EmptyCatalog.AddParts(parts);
            var configuration = CompositionConfiguration.Create(catalog);
            var exportProviderFactory = configuration.CreateExportProviderFactory();
            var exportProvider = exportProviderFactory.CreateExportProvider();
            var discoverablePart = exportProvider.GetExportedValue<DiscoverablePart1>();
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
                typeof(TypeWithMissingAttribute).GetTypeInfo().GetCustomAttributes(false);
                Skip.If(true, "The missing assembly is present. Test cannot verify proper operation.");
            }
            catch (FileNotFoundException) { }

            var result = await this.DiscoveryService.CreatePartsAsync(typeof(TypeWithMissingAttribute).GetTypeInfo().Assembly);

            // Verify that we still found parts.
            Assert.NotEqual(0, result.Parts.Count);
        }

        [SkippableFact]
        public async Task AssemblyDiscoveryDropsProblematicTypesAndSalvagesOthersInSameAssembly()
        {
            // If this assert fails, it means that the assembly that is supposed to be undiscoverable
            // by this unit test is actually discoverable. Check that CopyLocal=false for all references
            // to Microsoft.VisualStudio.Composition.MissingAssemblyTests and that the assembly
            // is not building to the same directory as the test assemblies.
            try
            {
                typeof(TypeWithMissingAttribute).GetTypeInfo().GetCustomAttributes(false);
                Skip.If(true, "The missing assembly is present. Test cannot verify proper operation.");
            }
            catch (FileNotFoundException) { }

            var result = await this.DiscoveryService.CreatePartsAsync(
                new List<Assembly>
                {
                    typeof(TypeWithMissingAttribute).GetTypeInfo().Assembly,
                    typeof(GoodType).GetTypeInfo().Assembly,
                });

            // Verify that the ReflectionTypeLoadException is logged.
            Assert.True(result.DiscoveryErrors.Any(ex => ex.InnerException is ReflectionTypeLoadException));

            // Verify that we still found parts in the bad and good assemblies.
            Assert.True(result.Parts.Any(p => p.Type == typeof(GoodPartInAssemblyWithBadTypes)));
            Assert.True(result.Parts.Any(p => p.Type == typeof(DiscoverablePart1)));
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

        #region Part Metadata tests

        /// <summary>
        /// Verifies that part metadata is available in the catalog.
        /// </summary>
        /// <remarks>
        /// Although part metadata is not used at runtime for the composition,
        /// some hosts such as VS may want to use it to filter the catalog
        /// before creating the composition.
        /// </remarks>
        [Fact]
        public void PartMetadataInCatalogIsPresent()
        {
            var part = this.DiscoveryService.CreatePart(typeof(SomePartWithPartMetadata));
            Assert.Equal("V1", part.Metadata["PM1"]);
            Assert.Equal("V2", part.Metadata["PM2"]);
        }

        /// <summary>
        /// Verifies that part metadata does not become export metadata.
        /// </summary>
        /// <remarks>
        /// This behavior isn't important, we're just documenting it.
        /// If we want to allow part metadata to be exposed through export metadata,
        /// simply update this test.
        /// </remarks>
        [Fact]
        public void PartMetadataInCatalogDoesNotPropagateToExportMetadata()
        {
            var part = this.DiscoveryService.CreatePart(typeof(SomePartWithPartMetadata));
            Assert.False(part.ExportDefinitions.Single().Value.Metadata.ContainsKey("PM1"));
        }

        [Fact]
        public void PartMetadataInCatalogOmitsBaseClassMetadata()
        {
            var part = this.DiscoveryService.CreatePart(typeof(SomePartWithPartMetadata));
            Assert.False(part.Metadata.ContainsKey("BasePM"));
        }

        [PartMetadata("BasePM", "V1"), MefV1.PartMetadata("BasePM", "V1")]
        public class BaseClassForPartWithMetadata { }

        [PartMetadata("PM1", "V1"), MefV1.PartMetadata("PM1", "V1")]
        [PartMetadata("PM2", "V2"), MefV1.PartMetadata("PM2", "V2")]
        public class SomePartWithPartMetadata : BaseClassForPartWithMetadata
        {
            [Export, MefV1.Export]
            public bool MemberExport { get { return true; } }
        }

        #endregion

        #region Type discovery failures

        [MefV1.Export, Export]
        public class SomePartWithoutImportingConstructor
        {
            public SomePartWithoutImportingConstructor(int foo) { }
        }

        #endregion

        /// <summary>
        /// A discovery mock that produces no parts, but includes all types for consideration.
        /// </summary>
        private class PartDiscoveryAllTypesMock : PartDiscovery
        {
            public PartDiscoveryAllTypesMock()
                : base(TestUtilities.Resolver)
            {
            }

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
