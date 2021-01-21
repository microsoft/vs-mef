// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
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
    }
}
