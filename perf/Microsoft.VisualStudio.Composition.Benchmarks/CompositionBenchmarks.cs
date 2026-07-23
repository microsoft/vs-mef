// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Benchmarks
{
    using System.IO;
    using System.Reflection;
    using BenchmarkDotNet.Attributes;
    using Microsoft.VisualStudio.Composition;
    using Microsoft.VSDiagnostics;

    [MemoryDiagnoser]
    [CPUUsageDiagnoser]
    public class CompositionBenchmarks
    {
        private Resolver resolver;
        private Assembly[] assemblies;
        private ComposableCatalog catalog;
        private CompositionConfiguration config;
        private RuntimeComposition runtime;
        private byte[] serialized;
        private IExportProviderFactory exportProviderFactory;

        [GlobalSetup]
        public void Setup()
        {
            this.resolver = Resolver.DefaultInstance;
            this.assemblies = new[] { typeof(Program).Assembly };

            var discovered = this.NewDiscovery().CreatePartsAsync(this.assemblies).GetAwaiter().GetResult();
            this.catalog = ComposableCatalog.Create(this.resolver).AddParts(discovered);
            this.config = CompositionConfiguration.Create(this.catalog);
            this.runtime = RuntimeComposition.CreateRuntimeComposition(this.config);

            using var ms = new MemoryStream();
            new CachedComposition().SaveAsync(this.runtime, ms).GetAwaiter().GetResult();
            this.serialized = ms.ToArray();

            this.exportProviderFactory = this.runtime.CreateExportProviderFactory();
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
            var exportProvider = this.exportProviderFactory.CreateExportProvider();
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

        private PartDiscovery NewDiscovery() => PartDiscovery.Combine(
            this.resolver,
            new AttributedPartDiscovery(this.resolver, isNonPublicSupported: true),
            new AttributedPartDiscoveryV1(this.resolver));
    }
}
