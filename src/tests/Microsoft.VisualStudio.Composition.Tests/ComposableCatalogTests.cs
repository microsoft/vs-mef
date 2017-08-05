// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class ComposableCatalogTests
    {
        [Fact]
        public async Task AddCatalog_MergesErrors()
        {
            var discovery = TestUtilities.V2Discovery;
            var emptyCatalog = ComposableCatalog.Create(discovery.Resolver);
            var result1 = emptyCatalog.AddParts(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist.dll" }));
            var result2 = emptyCatalog.AddParts(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist2.dll" }));

            var mergedCatalog = result1.AddCatalog(result2);

            Assert.Equal(result1.DiscoveredParts.DiscoveryErrors.Count + result2.DiscoveredParts.DiscoveryErrors.Count, mergedCatalog.DiscoveredParts.DiscoveryErrors.Count);
            Assert.NotEqual(0, mergedCatalog.DiscoveredParts.DiscoveryErrors.Count); // the test is ineffective otherwise.
        }

        [Fact]
        public async Task AddCatalogs_MergesErrors()
        {
            var discovery = TestUtilities.V2Discovery;
            var emptyCatalog = ComposableCatalog.Create(discovery.Resolver);
            var result1 = emptyCatalog.AddParts(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist.dll" }));
            var result2 = emptyCatalog.AddParts(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist2.dll" }));
            var result3 = emptyCatalog.AddParts(await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist3.dll" }));

            var mergedCatalog = result1.AddCatalogs(new[] { result2, result3 });

            Assert.Equal(
                result1.DiscoveredParts.DiscoveryErrors.Count + result2.DiscoveredParts.DiscoveryErrors.Count + result3.DiscoveredParts.DiscoveryErrors.Count,
                mergedCatalog.DiscoveredParts.DiscoveryErrors.Count);
            Assert.NotEqual(0, mergedCatalog.DiscoveredParts.DiscoveryErrors.Count); // the test is ineffective otherwise.
        }

        [Fact]
        public async Task MetadataAddedAfterDiscoveryIsAvailableAtRuntime()
        {
            var discovery = TestUtilities.V2Discovery;
            var parts = await discovery.CreatePartsAsync(typeof(Export1), typeof(Export2));
            var emptyCatalog = ComposableCatalog.Create(discovery.Resolver);
            var catalog = emptyCatalog.AddParts(parts);

            var modifiedParts = new DiscoveredParts(
                catalog.Parts.Select(p => new ComposablePartDefinition(
                    p.TypeRef,
                    p.Metadata,
                    p.ExportedTypes.Select(ed => new ExportDefinition(ed.ContractName, ImmutableDictionary.CreateRange(ed.Metadata).Add("K", "V"))).ToImmutableList(),
                    ImmutableDictionary.CreateRange(p.ExportingMembers.Select(kv => new KeyValuePair<MemberRef, IReadOnlyCollection<ExportDefinition>>(
                        kv.Key,
                        kv.Value.Select(ed => new ExportDefinition(ed.ContractName, ImmutableDictionary.CreateRange(ed.Metadata).Add("K", "V"))).ToImmutableList()))),
                    p.ImportingMembers,
                    p.SharingBoundary,
                    p.OnImportsSatisfiedRef,
                    p.ImportingConstructorOrFactoryRef,
                    p.ImportingConstructorImports,
                    p.CreationPolicy,
                    p.IsSharingBoundaryInferred)),
                catalog.DiscoveredParts.DiscoveryErrors);
            var modifiedCatalog = emptyCatalog.AddParts(modifiedParts);

            var exportProvider = CompositionConfiguration.Create(modifiedCatalog)
                .CreateExportProviderFactory()
                .CreateExportProvider();

            var export1 = exportProvider.GetExport<Export1, IDictionary<string, object>>();
            Assert.Equal("V", export1.Metadata["K"]);
            var export2 = exportProvider.GetExport<Export2, IDictionary<string, object>>();
            Assert.Equal("V", export2.Metadata["K"]);
        }

        [Export]
        public class Export1 { }

        [Export]
        public class Export2 { }
    }
}
