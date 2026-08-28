// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
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
                typeof(SharingBoundaryPart),
                typeof(SharingBoundarySimpleDependency));
            using ExportProvider provider = factory.CreateExportProvider();
            SharingBoundaryOwner owner = provider.GetExportedValue<SharingBoundaryOwner>();

            using (Export<SharingBoundaryPart> first = owner.Factory.CreateExport())
            {
                _ = first.Value;
                Assert.Equal(0, GetExpressionCompilationCount(factory));
            }

            using (Export<SharingBoundaryPart> second = owner.Factory.CreateExport())
            {
                Assert.NotNull(second.Value.Dependency);
                Assert.Equal(1, GetExpressionCompilationCount(factory));
                Assert.Equal(0, GetDirectActivationPlanCount(factory));
            }
        }

        /// <summary>
        /// Verifies that a direct activation plan resolves shared dependencies from the current boundary.
        /// </summary>
        [Fact]
        public void DirectActivationPlanPreservesBoundaryAndDisposalOwnership()
        {
            IExportProviderFactory factory = CreateBoundaryPlanFactory();
            ExportProvider provider = factory.CreateExportProvider();
            BoundaryPlanOwner owner = provider.GetExportedValue<BoundaryPlanOwner>();

            BoundaryScopedDependency firstScoped;
            GlobalSharedDependency global;
            using (Export<BoundaryGraphRoot> first = owner.GraphFactory.CreateExport())
            {
                BoundaryGraphRoot root = first.Value;
                Assert.Equal(0, GetDirectActivationPlanCount(factory));
                Assert.Same(root.Scoped, root.Leaf.Scoped);
                firstScoped = root.Scoped;
                global = root.Global;
                Assert.False(firstScoped.IsDisposed);
                Assert.False(global.IsDisposed);
            }

            Assert.True(firstScoped.IsDisposed);
            Assert.False(global.IsDisposed);

            BoundaryScopedDependency secondScoped;
            using (Export<BoundaryGraphRoot> second = owner.GraphFactory.CreateExport())
            {
                BoundaryGraphRoot root = second.Value;
                Assert.Equal(1, GetDirectActivationPlanCount(factory));
                Assert.Same(root.Scoped, root.Leaf.Scoped);
                Assert.NotSame(firstScoped, root.Scoped);
                Assert.Same(global, root.Global);
                secondScoped = root.Scoped;
                Assert.False(secondScoped.IsDisposed);
            }

            Assert.True(secondScoped.IsDisposed);
            Assert.False(global.IsDisposed);
            provider.Dispose();
            Assert.True(global.IsDisposed);
        }

        /// <summary>
        /// Verifies that concurrent boundary activations share one plan without sharing scoped instances.
        /// </summary>
        [Fact]
        public async Task DirectActivationPlanIsSafeAcrossConcurrentBoundaries()
        {
            IExportProviderFactory factory = CreateBoundaryPlanFactory();
            using ExportProvider provider = factory.CreateExportProvider();
            BoundaryPlanOwner owner = provider.GetExportedValue<BoundaryPlanOwner>();
            using (Export<BoundaryGraphRoot> first = owner.GraphFactory.CreateExport())
            {
                _ = first.Value;
            }

            BoundaryScopedDependency[] scopedDependencies = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(
                    _ => Task.Run(
                        () =>
                        {
                            using Export<BoundaryGraphRoot> export = owner.GraphFactory.CreateExport();
                            BoundaryGraphRoot root = export.Value;
                            Assert.Same(root.Scoped, root.Leaf.Scoped);
                            return root.Scoped;
                        })));

            Assert.Equal(scopedDependencies.Length, scopedDependencies.Distinct().Count());
            Assert.All(scopedDependencies, scoped => Assert.True(scoped.IsDisposed));
            Assert.Equal(1, GetDirectActivationPlanCount(factory));
        }

        /// <summary>
        /// Verifies that a shared plan does not retain values from its first boundary provider.
        /// </summary>
        [Fact]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void DirectActivationPlanDoesNotRetainDisposedBoundary()
        {
            IExportProviderFactory factory = CreateBoundaryPlanFactory();
            using ExportProvider provider = factory.CreateExportProvider();
            BoundaryPlanOwner owner = provider.GetExportedValue<BoundaryPlanOwner>();

            WeakReference scopedDependency = CreateAndDisposeCompiledBoundary(owner);
            Assert.Equal(1, GetDirectActivationPlanCount(factory));

            GC.Collect();
            Assert.False(scopedDependency.IsAlive);
        }

        /// <summary>
        /// Verifies that phased direct plans preserve property-import cycles within each boundary.
        /// </summary>
        [Fact]
        public void DirectActivationPlanPreservesPropertyImportCycles()
        {
            IExportProviderFactory factory = CreateBoundaryPlanFactory();
            using ExportProvider provider = factory.CreateExportProvider();
            BoundaryPlanOwner owner = provider.GetExportedValue<BoundaryPlanOwner>();

            using (Export<BoundaryCycleA> first = owner.CycleFactory.CreateExport())
            {
                Assert.Same(first.Value, first.Value.B.A);
            }

            using (Export<BoundaryCycleA> second = owner.CycleFactory.CreateExport())
            {
                Assert.Same(second.Value, second.Value.B.A);
                Assert.True(GetDirectActivationPlanCount(factory) >= 1);
            }
        }

        /// <summary>
        /// Verifies that disposable boundary roots remain on the lifecycle path.
        /// </summary>
        [Fact]
        public void DisposableBoundaryRootDoesNotUseDirectActivationPlan()
        {
            IExportProviderFactory factory = CreateBoundaryPlanFactory();
            using ExportProvider provider = factory.CreateExportProvider();
            BoundaryPlanOwner owner = provider.GetExportedValue<BoundaryPlanOwner>();

            DisposableBoundaryRoot firstValue;
            using (Export<DisposableBoundaryRoot> first = owner.DisposableFactory.CreateExport())
            {
                firstValue = first.Value;
                Assert.False(firstValue.IsDisposed);
            }

            Assert.True(firstValue.IsDisposed);

            DisposableBoundaryRoot secondValue;
            using (Export<DisposableBoundaryRoot> second = owner.DisposableFactory.CreateExport())
            {
                secondValue = second.Value;
                Assert.False(secondValue.IsDisposed);
            }

            Assert.True(secondValue.IsDisposed);
            Assert.Equal(0, GetDirectActivationPlanCount(factory));
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

        private static IExportProviderFactory CreateBoundaryPlanFactory()
        {
            return CreateFactory(
                ExportProviderFactoryOptions.EnableActivationExpressionCompilation,
                typeof(BoundaryPlanOwner),
                typeof(BoundaryGraphRoot),
                typeof(BoundaryGraphLeaf),
                typeof(BoundaryScopedDependency),
                typeof(GlobalSharedDependency),
                typeof(BoundaryCycleA),
                typeof(BoundaryCycleB),
                typeof(BoundaryCycleHelperA),
                typeof(BoundaryCycleHelperB),
                typeof(DisposableBoundaryRoot));
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

        private static int GetDirectActivationPlanCount(IExportProviderFactory factory)
        {
            PropertyInfo property = factory.GetType().GetProperty("DirectActivationPlanCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (int)property.GetValue(factory)!;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndDisposeCompiledBoundary(BoundaryPlanOwner owner)
        {
            using (Export<BoundaryGraphRoot> first = owner.GraphFactory.CreateExport())
            {
                _ = first.Value;
            }

            using (Export<BoundaryGraphRoot> second = owner.GraphFactory.CreateExport())
            {
                return new WeakReference(second.Value.Scoped);
            }
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
            [Import]
            internal SharingBoundarySimpleDependency Dependency { get; set; } = null!;
        }

        [Export, Shared]
        private sealed class SharingBoundarySimpleDependency
        {
        }

        [Export, Shared]
        private sealed class BoundaryPlanOwner
        {
            [Import, SharingBoundary("SharedPlanBoundary")]
            internal ExportFactory<BoundaryGraphRoot> GraphFactory { get; set; } = null!;

            [Import, SharingBoundary("SharedPlanBoundary")]
            internal ExportFactory<BoundaryCycleA> CycleFactory { get; set; } = null!;

            [Import, SharingBoundary("SharedPlanBoundary")]
            internal ExportFactory<DisposableBoundaryRoot> DisposableFactory { get; set; } = null!;
        }

        [Export, Shared("SharedPlanBoundary")]
        private sealed class BoundaryGraphRoot
        {
            [ImportingConstructor]
            internal BoundaryGraphRoot(BoundaryGraphLeaf leaf)
            {
                this.Leaf = leaf;
            }

            internal BoundaryGraphLeaf Leaf { get; }

            [Import]
            internal BoundaryScopedDependency Scoped { get; set; } = null!;

            [Import]
            internal GlobalSharedDependency Global { get; set; } = null!;
        }

        [Export]
        private sealed class BoundaryGraphLeaf
        {
            [ImportingConstructor]
            internal BoundaryGraphLeaf(BoundaryScopedDependency scoped)
            {
                this.Scoped = scoped;
            }

            internal BoundaryScopedDependency Scoped { get; }
        }

        [Export, Shared("SharedPlanBoundary")]
        private sealed class BoundaryScopedDependency : IDisposable
        {
            internal bool IsDisposed { get; private set; }

            public void Dispose() => this.IsDisposed = true;
        }

        [Export, Shared]
        private sealed class GlobalSharedDependency : IDisposable
        {
            internal bool IsDisposed { get; private set; }

            public void Dispose() => this.IsDisposed = true;
        }

        [Export, Shared("SharedPlanBoundary")]
        private sealed class BoundaryCycleA
        {
            [Import]
            internal BoundaryCycleB B { get; set; } = null!;

            [Import]
            internal BoundaryCycleHelperA HelperA { get; set; } = null!;

            [Import]
            internal BoundaryCycleHelperB HelperB { get; set; } = null!;
        }

        [Export, Shared("SharedPlanBoundary")]
        private sealed class BoundaryCycleB
        {
            [Import]
            internal BoundaryCycleA A { get; set; } = null!;
        }

        [Export]
        private sealed class BoundaryCycleHelperA
        {
        }

        [Export]
        private sealed class BoundaryCycleHelperB
        {
        }

        [Export, Shared("SharedPlanBoundary")]
        private sealed class DisposableBoundaryRoot : IDisposable
        {
            internal bool IsDisposed { get; private set; }

            public void Dispose() => this.IsDisposed = true;
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
