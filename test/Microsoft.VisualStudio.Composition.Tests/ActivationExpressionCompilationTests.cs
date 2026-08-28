// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// Tests tiered expression compilation for repeated activation.
    /// </summary>
    public class ActivationExpressionCompilationTests
    {
        /// <summary>
        /// Verifies that expression compilation is disabled by default.
        /// </summary>
        [Fact]
        public void ExpressionCompilationIsDisabledByDefault()
        {
            IExportProviderFactory factory = CreateFactory(ExportProviderFactoryOptions.None, typeof(NonSharedPart));
            using ExportProvider provider = factory.CreateExportProvider();

            _ = provider.GetExportedValue<NonSharedPart>();
            _ = provider.GetExportedValue<NonSharedPart>();

            Assert.Equal(0, GetExpressionCompilationCount(factory));
        }

        /// <summary>
        /// Verifies that a non-shared part compiles only after repeated activation.
        /// </summary>
        [Fact]
        public void NonSharedPartCompilesOnSecondActivation()
        {
            IExportProviderFactory factory = CreateFactory(
                ExportProviderFactoryOptions.EnableActivationExpressionCompilation,
                typeof(NonSharedPart));
            using ExportProvider provider = factory.CreateExportProvider();

            _ = provider.GetExportedValue<NonSharedPart>();
            Assert.Equal(0, GetExpressionCompilationCount(factory));

            _ = provider.GetExportedValue<NonSharedPart>();
            Assert.Equal(1, GetExpressionCompilationCount(factory));

            _ = provider.GetExportedValue<NonSharedPart>();
            Assert.Equal(1, GetExpressionCompilationCount(factory));
        }

        /// <summary>
        /// Verifies that application-wide shared parts are never expression compiled.
        /// </summary>
        [Fact]
        public void ApplicationSharedPartDoesNotCompile()
        {
            IExportProviderFactory factory = CreateFactory(
                ExportProviderFactoryOptions.EnableActivationExpressionCompilation,
                typeof(ApplicationSharedPart));
            using ExportProvider provider = factory.CreateExportProvider();

            _ = provider.GetExportedValue<ApplicationSharedPart>();
            _ = provider.GetExportedValue<ApplicationSharedPart>();

            Assert.Equal(0, GetExpressionCompilationCount(factory));
        }

        /// <summary>
        /// Verifies that activation counts and compiled delegates are shared across sharing-boundary instances.
        /// </summary>
        [Fact]
        public void SharingBoundaryPartCompilesOnSecondBoundaryInstance()
        {
            IExportProviderFactory factory = CreateFactory(
                ExportProviderFactoryOptions.EnableActivationExpressionCompilation,
                typeof(SharingBoundaryOwner),
                typeof(SharingBoundaryPart));
            using ExportProvider provider = factory.CreateExportProvider();
            SharingBoundaryOwner owner = provider.GetExportedValue<SharingBoundaryOwner>();

            using (Export<SharingBoundaryPart> first = owner.Factory.CreateExport())
            {
                _ = first.Value;
                Assert.Equal(0, GetExpressionCompilationCount(factory));
            }

            using (Export<SharingBoundaryPart> second = owner.Factory.CreateExport())
            {
                _ = second.Value;
                Assert.Equal(1, GetExpressionCompilationCount(factory));
            }
        }

        /// <summary>
        /// Verifies that a compiled direct plan correctly satisfies constructor, property, and many imports.
        /// </summary>
        [Fact]
        public void CompiledDirectPlanSatisfiesImports()
        {
            IExportProviderFactory factory = CreateFactory(
                ExportProviderFactoryOptions.EnableActivationExpressionCompilation,
                typeof(ImportedPart),
                typeof(ImportedProperty),
                typeof(FirstAdapter),
                typeof(SecondAdapter));
            using ExportProvider provider = factory.CreateExportProvider();

            ImportedPart first = provider.GetExportedValue<ImportedPart>();
            Assert.Equal(0, GetExpressionCompilationCount(factory));
            Assert.IsType<ImportedProperty>(first.Property);
            Assert.Contains(first.Adapters, adapter => adapter is FirstAdapter);
            Assert.Contains(first.Adapters, adapter => adapter is SecondAdapter);

            ImportedPart second = provider.GetExportedValue<ImportedPart>();
            Assert.NotSame(first, second);
            Assert.True(GetExpressionCompilationCount(factory) > 0);
            Assert.IsType<ImportedProperty>(second.Property);
            Assert.Contains(second.Adapters, adapter => adapter is FirstAdapter);
            Assert.Contains(second.Adapters, adapter => adapter is SecondAdapter);
        }

        /// <summary>
        /// Verifies that undefined options are rejected.
        /// </summary>
        [Fact]
        public void UndefinedOptionIsRejected()
        {
            var configuration = CompositionConfiguration.Create(TestUtilities.EmptyCatalog);
            RuntimeComposition composition = RuntimeComposition.CreateRuntimeComposition(configuration);

            Assert.Throws<ArgumentException>(() => composition.CreateExportProviderFactory((ExportProviderFactoryOptions)0x2));
        }

        /// <summary>
        /// Verifies that cached-composition loading propagates expression compilation options.
        /// </summary>
        [Fact]
        public async Task CachedCompositionPropagatesOptions()
        {
            CompositionConfiguration configuration = CreateConfiguration(typeof(NonSharedPart));
            var cache = new CachedComposition();
            using var stream = new MemoryStream();
            await cache.SaveAsync(configuration, stream);
            stream.Position = 0;

            IExportProviderFactory factory = await cache.LoadExportProviderFactoryAsync(
                stream,
                Resolver.DefaultInstance,
                ExportProviderFactoryOptions.EnableActivationExpressionCompilation);
            using ExportProvider provider = factory.CreateExportProvider();
            _ = provider.GetExportedValue<NonSharedPart>();
            _ = provider.GetExportedValue<NonSharedPart>();

            Assert.Equal(1, GetExpressionCompilationCount(factory));
        }

        private static IExportProviderFactory CreateFactory(ExportProviderFactoryOptions options, params Type[] partTypes)
        {
            RuntimeComposition composition = RuntimeComposition.CreateRuntimeComposition(CreateConfiguration(partTypes));
            return composition.CreateExportProviderFactory(options);
        }

        private static CompositionConfiguration CreateConfiguration(params Type[] partTypes)
        {
            var resolver = Resolver.DefaultInstance;
            var discovery = new AttributedPartDiscovery(resolver, isNonPublicSupported: true);
            DiscoveredParts discoveredParts = discovery.CreatePartsAsync(partTypes).GetAwaiter().GetResult();
            ComposableCatalog catalog = ComposableCatalog.Create(resolver).AddParts(discoveredParts);
            return CompositionConfiguration.Create(catalog);
        }

        private static int GetExpressionCompilationCount(IExportProviderFactory factory)
        {
            PropertyInfo property = factory.GetType().GetProperty("ExpressionCompilationCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (int)property.GetValue(factory)!;
        }

        [Export]
        private sealed class NonSharedPart
        {
        }

        [Export, Shared]
        private sealed class ApplicationSharedPart
        {
        }

        [Export, Shared]
        private sealed class SharingBoundaryOwner
        {
            [Import, SharingBoundary("TestBoundary")]
            internal ExportFactory<SharingBoundaryPart> Factory { get; set; } = null!;
        }

        [Export, Shared("TestBoundary")]
        private sealed class SharingBoundaryPart
        {
        }

        [Export]
        private sealed class ImportedPart
        {
            [ImportingConstructor]
            internal ImportedPart([ImportMany] IEnumerable<IAdapter> adapters)
            {
                this.Adapters = adapters;
            }

            internal IEnumerable<IAdapter> Adapters { get; }

            [Import]
            internal ImportedProperty Property { get; set; } = null!;
        }

        [Export]
        private sealed class ImportedProperty
        {
        }

        private interface IAdapter
        {
        }

        [Export(typeof(IAdapter))]
        private sealed class FirstAdapter : IAdapter
        {
        }

        [Export(typeof(IAdapter))]
        private sealed class SecondAdapter : IAdapter
        {
        }
    }
}
