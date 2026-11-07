// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Benchmarks;

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.VisualStudio.Composition.Reflection;

/// <summary>
/// Measures repeated reads of lazily translated metadata from a serialized composition.
/// </summary>
[MemoryDiagnoser]
public class MetadataReadBenchmarks
{
    private IReadOnlyDictionary<string, object?> metadata = null!;

    private enum MetadataValue
    {
        First,
        Second,
    }

    /// <summary>
    /// Deserializes and warms metadata before measuring repeated reads.
    /// </summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        Resolver resolver = Resolver.DefaultInstance;
        TypeRef typeRef = TypeRef.Get(typeof(MetadataReadBenchmarks), resolver);
        ImmutableDictionary<string, object?> values = ImmutableDictionary<string, object?>.Empty
            .Add("TypeArray", new Type[] { typeof(string), typeof(int) })
            .Add("Enum", MetadataValue.First)
            .Add("EnumArray", new[] { MetadataValue.First, MetadataValue.Second });
        var export = new RuntimeComposition.RuntimeExport("Contract", typeRef, memberRef: null, values);
        var part = new RuntimeComposition.RuntimePart(
            typeRef,
            importingConstructor: null,
            importingConstructorArguments: Array.Empty<RuntimeComposition.RuntimeImport>(),
            importingMembers: Array.Empty<RuntimeComposition.RuntimeImport>(),
            exports: new[] { export },
            onImportsSatisfiedMethods: Array.Empty<MethodRef>(),
            sharingBoundary: null);
        RuntimeComposition composition = RuntimeComposition.CreateRuntimeComposition(
            new[] { part },
            ImmutableDictionary<TypeRef, RuntimeComposition.RuntimeExport>.Empty.Add(typeRef, export),
            resolver);

        var cache = new CachedComposition();
        using var stream = new MemoryStream();
        await cache.SaveAsync(composition, stream);
        stream.Position = 0;
        RuntimeComposition deserializedComposition = await cache.LoadRuntimeCompositionAsync(stream, resolver);
        this.metadata = deserializedComposition.GetExports("Contract").Single().Metadata;

        if (!((Type[])this.TypeArray()).SequenceEqual(new[] { typeof(string), typeof(int) })
            || !Equals(MetadataValue.First, this.Enum())
            || !((MetadataValue[])this.EnumArray()).SequenceEqual(new[] { MetadataValue.First, MetadataValue.Second }))
        {
            throw new InvalidOperationException("Metadata translation changed the benchmark's values.");
        }
    }

    /// <summary>
    /// Reads a previously resolved array of types.
    /// </summary>
    /// <returns>The metadata array.</returns>
    [Benchmark]
    public object TypeArray() => this.metadata["TypeArray"]!;

    /// <summary>
    /// Reads a previously resolved enum value.
    /// </summary>
    /// <returns>The boxed metadata value.</returns>
    [Benchmark]
    public object Enum() => this.metadata["Enum"]!;

    /// <summary>
    /// Reads a previously resolved array of enum values.
    /// </summary>
    /// <returns>The metadata array.</returns>
    [Benchmark]
    public object EnumArray() => this.metadata["EnumArray"]!;
}
