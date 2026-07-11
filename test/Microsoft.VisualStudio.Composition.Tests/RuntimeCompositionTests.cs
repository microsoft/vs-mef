// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class RuntimeCompositionTests
    {
        [Fact]
        public void TestEmptyCatalogTest()
        {
            var configuration = CompositionConfiguration.Create(TestUtilities.EmptyCatalog);
            var composition = RuntimeComposition.CreateRuntimeComposition(configuration);
            var factory = composition.CreateExportProviderFactory();
            var provider = factory.CreateExportProvider();
            var exports = provider.GetExports<IDisposable>();
            Assert.Empty(exports);
        }

        [Fact]
        public void TestEmptyPartsThrowsException()
        {
            var configuration = CompositionConfiguration.Create(TestUtilities.EmptyCatalog);
            var validComposition = RuntimeComposition.CreateRuntimeComposition(configuration);

            Assert.Throws<ArgumentException>(() => RuntimeComposition.CreateRuntimeComposition(Enumerable.Empty<RuntimeComposition.RuntimePart>(), validComposition.MetadataViewsAndProviders, Resolver.DefaultInstance));
        }

        [Fact]
        public void TestEmptyMetadataViewProviderThrowsException()
        {
            var configuration = CompositionConfiguration.Create(TestUtilities.EmptyCatalog);
            var validComposition = RuntimeComposition.CreateRuntimeComposition(configuration);

            Assert.Throws<ArgumentException>(() => RuntimeComposition.CreateRuntimeComposition(validComposition.Parts, ImmutableDictionary<TypeRef, RuntimeComposition.RuntimeExport>.Empty, Resolver.DefaultInstance));
        }

        /// <summary>
        /// Verifies that export indexing preserves each contract's exports and their order.
        /// </summary>
        [Fact]
        public void GetExportsGroupsExportsByContractName()
        {
            const string SharedContract = "Shared";
            const string UniqueContract = "Unique";
            RuntimeComposition.RuntimePart[] parts =
            {
                CreatePart(typeof(string), SharedContract, UniqueContract),
                CreatePart(typeof(int), SharedContract),
            };

            RuntimeComposition composition = CreateComposition(parts);

            RuntimeComposition.RuntimeExport[] expectedSharedExports = composition.Parts
                .SelectMany(part => part.Exports)
                .Where(export => export.ContractName == SharedContract)
                .ToArray();
            RuntimeComposition.RuntimeExport[] expectedUniqueExports = composition.Parts
                .SelectMany(part => part.Exports)
                .Where(export => export.ContractName == UniqueContract)
                .ToArray();
            Assert.Equal(expectedSharedExports, composition.GetExports(SharedContract));
            Assert.Equal(expectedUniqueExports, composition.GetExports(UniqueContract));
            Assert.Empty(composition.GetExports("Missing"));
        }

        /// <summary>
        /// Verifies that deserialized empty metadata uses the shared empty dictionary.
        /// </summary>
        [Fact]
        public async Task EmptyMetadataIsSharedAfterDeserializationAsync()
        {
            RuntimeComposition composition = CreateComposition(
                CreatePart(typeof(string), "Contract1"),
                CreatePart(typeof(int), "Contract2"));

            RuntimeComposition deserializedComposition = await RoundTripAsync(composition);

            IReadOnlyDictionary<string, object?>[] metadataDictionaries = deserializedComposition.Parts
                .SelectMany(part => part.Exports)
                .Select(export => export.Metadata)
                .Concat(deserializedComposition.MetadataViewsAndProviders.Values.Select(export => export.Metadata))
                .ToArray();
            Assert.NotEmpty(metadataDictionaries);
            Assert.All(metadataDictionaries, Assert.Empty);
            Assert.All(metadataDictionaries.Skip(1), metadata => Assert.Same(metadataDictionaries[0], metadata));
        }

        /// <summary>
        /// Verifies that metadata without substituted values is returned directly.
        /// </summary>
        [Fact]
        public async Task MetadataWithoutSubstitutedValuesIsImmutableDictionaryAsync()
        {
            ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty
                .Add("String", "value")
                .Add("Integer", 42);
            RuntimeComposition composition = CreateComposition(CreatePart(typeof(string), "Contract", metadata));

            RuntimeComposition deserializedComposition = await RoundTripAsync(composition);
            IReadOnlyDictionary<string, object?> deserializedMetadata = deserializedComposition.GetExports("Contract").Single().Metadata;

            Assert.IsAssignableFrom<ImmutableDictionary<string, object?>>(deserializedMetadata);
            Assert.Equal("value", deserializedMetadata["String"]);
            Assert.Equal(42, deserializedMetadata["Integer"]);
        }

        /// <summary>
        /// Verifies that metadata containing substituted values retains conversion behavior.
        /// </summary>
        [Fact]
        public async Task MetadataWithSubstitutedValuesIsResolvedAsync()
        {
            ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty.Add("Type", typeof(IDisposable));
            RuntimeComposition composition = CreateComposition(CreatePart(typeof(string), "Contract", metadata));

            RuntimeComposition deserializedComposition = await RoundTripAsync(composition);
            IReadOnlyDictionary<string, object?> deserializedMetadata = deserializedComposition.GetExports("Contract").Single().Metadata;

            Assert.False(deserializedMetadata is ImmutableDictionary<string, object?>);
            Assert.Equal(typeof(IDisposable), deserializedMetadata["Type"]);
        }

        /// <summary>
        /// Verifies that common metadata array types are preserved during deserialization.
        /// </summary>
        [Fact]
        public async Task CommonMetadataArrayTypesArePreservedAfterDeserializationAsync()
        {
            ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty
                .Add("ObjectArray", new object?[] { "value", 1, null })
                .Add("StringArray", new string?[] { "value", null })
                .Add("TypeArray", new Type[] { typeof(string), typeof(int) });
            RuntimeComposition composition = CreateComposition(CreatePart(typeof(string), "Contract", metadata));

            RuntimeComposition deserializedComposition = await RoundTripAsync(composition);
            IReadOnlyDictionary<string, object?> deserializedMetadata = deserializedComposition.GetExports("Contract").Single().Metadata;

            Assert.Equal(new object?[] { "value", 1, null }, Assert.IsType<object[]>(deserializedMetadata["ObjectArray"]));
            Assert.Equal(new string?[] { "value", null }, Assert.IsType<string[]>(deserializedMetadata["StringArray"]));
            Assert.Equal(new Type[] { typeof(string), typeof(int) }, Assert.IsType<Type[]>(deserializedMetadata["TypeArray"]));
        }

        /// <summary>
        /// Verifies that frequently occurring metadata values reuse cached boxed instances.
        /// </summary>
        [Fact]
        public async Task CommonMetadataValuesReuseBoxedInstancesAfterDeserializationAsync()
        {
            ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty
                .Add("Any1", CreationPolicy.Any)
                .Add("Any2", CreationPolicy.Any)
                .Add("Shared1", CreationPolicy.Shared)
                .Add("Shared2", CreationPolicy.Shared)
                .Add("NonShared1", CreationPolicy.NonShared)
                .Add("NonShared2", CreationPolicy.NonShared)
                .Add("NegativeOne1", -1)
                .Add("NegativeOne2", -1)
                .Add("Zero1", 0)
                .Add("Zero2", 0)
                .Add("One1", 1)
                .Add("One2", 1)
                .Add("Two1", 2)
                .Add("Two2", 2);
            RuntimeComposition composition = CreateComposition(CreatePart(typeof(string), "Contract", metadata));

            RuntimeComposition deserializedComposition = await RoundTripAsync(composition);
            IReadOnlyDictionary<string, object?> deserializedMetadata = deserializedComposition.GetExports("Contract").Single().Metadata;

            Assert.Same(deserializedMetadata["Any1"], deserializedMetadata["Any2"]);
            Assert.Same(deserializedMetadata["Shared1"], deserializedMetadata["Shared2"]);
            Assert.Same(deserializedMetadata["NonShared1"], deserializedMetadata["NonShared2"]);
            Assert.Same(deserializedMetadata["NegativeOne1"], deserializedMetadata["NegativeOne2"]);
            Assert.Same(deserializedMetadata["Zero1"], deserializedMetadata["Zero2"]);
            Assert.Same(deserializedMetadata["One1"], deserializedMetadata["One2"]);
            Assert.NotSame(deserializedMetadata["Two1"], deserializedMetadata["Two2"]);
        }

        private static RuntimeComposition.RuntimePart CreatePart(Type type, params string[] contractNames)
        {
            return CreatePart(type, contractNames, ImmutableDictionary<string, object?>.Empty);
        }

        private static RuntimeComposition.RuntimePart CreatePart(Type type, string contractName, IReadOnlyDictionary<string, object?> metadata)
        {
            return CreatePart(type, new[] { contractName }, metadata);
        }

        private static RuntimeComposition.RuntimePart CreatePart(Type type, IReadOnlyList<string> contractNames, IReadOnlyDictionary<string, object?> metadata)
        {
            TypeRef typeRef = TypeRef.Get(type, TestUtilities.Resolver);
            RuntimeComposition.RuntimeExport[] exports = contractNames
                .Select(contractName => new RuntimeComposition.RuntimeExport(contractName, typeRef, memberRef: null, metadata))
                .ToArray();
            return new RuntimeComposition.RuntimePart(
                typeRef,
                importingConstructor: null,
                importingConstructorArguments: Array.Empty<RuntimeComposition.RuntimeImport>(),
                importingMembers: Array.Empty<RuntimeComposition.RuntimeImport>(),
                exports,
                onImportsSatisfiedMethods: Array.Empty<MethodRef>(),
                sharingBoundary: null);
        }

        private static RuntimeComposition CreateComposition(params RuntimeComposition.RuntimePart[] parts)
        {
            TypeRef providerTypeRef = TypeRef.Get(typeof(RuntimeCompositionTests), TestUtilities.Resolver);
            var providerExport = new RuntimeComposition.RuntimeExport(
                "MetadataProvider",
                providerTypeRef,
                memberRef: null,
                ImmutableDictionary<string, object?>.Empty);
            ImmutableDictionary<TypeRef, RuntimeComposition.RuntimeExport> metadataViewsAndProviders =
                ImmutableDictionary<TypeRef, RuntimeComposition.RuntimeExport>.Empty.Add(providerTypeRef, providerExport);
            return RuntimeComposition.CreateRuntimeComposition(parts, metadataViewsAndProviders, TestUtilities.Resolver);
        }

        private static async Task<RuntimeComposition> RoundTripAsync(RuntimeComposition composition)
        {
            var cache = new CachedComposition();
            using var stream = new MemoryStream();
            await cache.SaveAsync(composition, stream);
            stream.Position = 0;
            return await cache.LoadRuntimeCompositionAsync(stream, TestUtilities.Resolver);
        }
    }
}
