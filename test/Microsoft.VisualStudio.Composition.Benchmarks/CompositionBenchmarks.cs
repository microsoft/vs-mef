// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Benchmarks;

using System.Collections.Immutable;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

[MemoryDiagnoser]
[CPUUsageDiagnoser]
public class CompositionBenchmarks
{
    private Resolver resolver = null!;
    private Assembly[] assemblies = null!;
    private ComposableCatalog catalog = null!;
    private RuntimeComposition runtime = null!;
    private byte[] serialized = null!;
    private IExportProviderFactory exportProviderFactory = null!;
    private ImportDefinition unconstrainedImport = null!;
    private ImportDefinition constrainedImport = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.resolver = Resolver.DefaultInstance;
        this.assemblies = new[] { typeof(CompositionBenchmarks).Assembly };

        var discovered = this.NewDiscovery().CreatePartsAsync(this.assemblies).GetAwaiter().GetResult();
        this.catalog = ComposableCatalog.Create(this.resolver).AddParts(discovered);
        var config = CompositionConfiguration.Create(this.catalog);
        this.runtime = RuntimeComposition.CreateRuntimeComposition(config);

        using var ms = new MemoryStream();
        new CachedComposition().SaveAsync(this.runtime, ms).GetAwaiter().GetResult();
        this.serialized = ms.ToArray();

        this.exportProviderFactory = this.runtime.CreateExportProviderFactory();

        // Two imports for the catalog's most-exported contract: one with no export constraints and
        // one with a type-identity constraint. Both resolve the same exports, so the delta between
        // the GetExports* benchmarks isolates (and guards) the no-constraint fast path.
        string contractName = this.catalog.Parts
            .SelectMany(p => p.ExportedTypes)
            .GroupBy(e => e.ContractName)
            .OrderByDescending(g => g.Count())
            .First().Key;
        var emptyMetadata = ImmutableDictionary<string, object?>.Empty;
        this.unconstrainedImport = new ImportDefinition(
            contractName,
            ImportCardinality.ZeroOrMore,
            emptyMetadata,
            ImmutableList<IImportSatisfiabilityConstraint>.Empty);
        this.constrainedImport = new ImportDefinition(
            contractName,
            ImportCardinality.ZeroOrMore,
            emptyMetadata,
            new IImportSatisfiabilityConstraint[] { new ExportTypeIdentityConstraint(typeof(IService)) });
    }

    [Benchmark]
    public int Discovery()
    {
        var discovered = this.NewDiscovery().CreatePartsAsync(this.assemblies).GetAwaiter().GetResult();
        return discovered.Parts.Count;
    }

    [Benchmark]
    public int Composition()
    {
        var configuration = CompositionConfiguration.Create(this.catalog);
        return configuration.Parts.Count;
    }

    [Benchmark]
    public long Serialize()
    {
        using var ms = new MemoryStream(this.serialized.Length);
        new CachedComposition().SaveAsync(this.runtime, ms).GetAwaiter().GetResult();
        return ms.Length;
    }

    [Benchmark]
    public int Deserialize()
    {
        using var ms = new MemoryStream(this.serialized, writable: false);
        var loaded = new CachedComposition().LoadRuntimeCompositionAsync(ms, this.resolver).GetAwaiter().GetResult();
        return loaded.Parts.Count;
    }

    [Benchmark]
    public int Runtime()
    {
        int count = 0;
        using var exportProvider = this.exportProviderFactory.CreateExportProvider();
        foreach (var processor in exportProvider.GetExportedValues<IProcessor>())
        {
            count++;
        }

        foreach (var service in exportProvider.GetExportedValues<IService>())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int GetExportsUnconstrained() => this.catalog.GetExports(this.unconstrainedImport).Count;

    [Benchmark]
    public int GetExportsConstrained() => this.catalog.GetExports(this.constrainedImport).Count;

    private PartDiscovery NewDiscovery() => PartDiscovery.Combine(
        this.resolver,
        new AttributedPartDiscovery(this.resolver, isNonPublicSupported: true),
        new AttributedPartDiscoveryV1(this.resolver));
}
