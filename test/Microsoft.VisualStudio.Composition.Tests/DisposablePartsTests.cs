// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Threading;
    using Xunit;
    using IAsyncDisposable = System.IAsyncDisposable;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Disposal", "")]
    public class DisposablePartsTests
    {
        [Fact]
        public async Task AsyncDisposablePartDisposedWithExportProviderAsync()
        {
            AsyncDisposablePart.DisposalCount = 0;
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(AsyncDisposablePart));
            exportProvider.GetExportedValue<AsyncDisposablePart>();

            await exportProvider.DisposeAsync();
            await exportProvider.DisposeAsync();

            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
        }

        [Fact]
        public async Task AsyncDisposalPrefersIAsyncDisposableOverIDisposable()
        {
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(DualDisposablePart));
            DualDisposablePart part = exportProvider.GetExportedValue<DualDisposablePart>();

            await exportProvider.DisposeAsync();

            Assert.True(part.DisposeAsyncCalled);
            Assert.False(part.DisposeCalled);
        }

        [Fact]
        public async Task SynchronousDisposalPrefersIDisposableWithoutJoinableTaskFactory()
        {
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(DualDisposablePart));
            DualDisposablePart part = exportProvider.GetExportedValue<DualDisposablePart>();

            exportProvider.Dispose();

            Assert.True(part.DisposeCalled);
            Assert.False(part.DisposeAsyncCalled);
        }

        [Fact]
        public async Task SynchronousDisposalPrefersIAsyncDisposableWithJoinableTaskFactory()
        {
            using var joinableTaskContext = new JoinableTaskContext();
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskContext.Factory, typeof(DualDisposablePart));
            DualDisposablePart part = exportProvider.GetExportedValue<DualDisposablePart>();

            exportProvider.Dispose();

            Assert.False(part.DisposeCalled);
            Assert.True(part.DisposeAsyncCalled);
        }

        [Fact]
        public async Task DelegatingExportProviderInheritsJoinableTaskFactory()
        {
            using var joinableTaskContext = new JoinableTaskContext();
            ExportProvider innerExportProvider = await CreateExportProviderAsync(joinableTaskContext.Factory);
            var part = new DualDisposablePart();
            var exportProvider = new PartOwningDelegatingExportProvider(innerExportProvider, part);

            exportProvider.Dispose();
            innerExportProvider.Dispose();

            Assert.False(part.DisposeCalled);
            Assert.True(part.DisposeAsyncCalled);
        }

        [Fact]
        public async Task LateTrackedPartUsesJoinableTaskFactoryDisposalPolicy()
        {
            using var joinableTaskContext = new JoinableTaskContext();
            ExportProvider innerExportProvider = await CreateExportProviderAsync(joinableTaskContext.Factory);
            var exportProvider = new PartOwningDelegatingExportProvider(innerExportProvider, new DualDisposablePart());
            var latePart = new DualDisposablePart();
            exportProvider.Dispose();

            exportProvider.TrackPart(latePart);
            innerExportProvider.Dispose();

            Assert.False(latePart.DisposeCalled);
            Assert.True(latePart.DisposeAsyncCalled);
        }

        [Fact]
        public async Task ConcurrentDisposalCallsAwaitSameOperation()
        {
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(AsyncDisposalGatePart));
            AsyncDisposalGatePart part = exportProvider.GetExportedValue<AsyncDisposalGatePart>();

            Task firstDisposal = exportProvider.DisposeAsync().AsTask();
#pragma warning disable VSTHRD003 // This test intentionally awaits signals initiated by the disposal operation.
            await part.DisposalStarted.Task;
#pragma warning restore VSTHRD003
            Task secondDisposal = exportProvider.DisposeAsync().AsTask();

            Assert.False(secondDisposal.IsCompleted);
            part.AllowDisposalToComplete.SetResult(null);
            await Task.WhenAll(firstDisposal, secondDisposal);
            Assert.Equal(1, part.DisposalCount);
        }

        [Fact]
        public async Task DisposeAsyncInvokesDerivedDisposeOverride()
        {
            ExportProvider innerExportProvider = await CreateExportProviderAsync(joinableTaskFactory: null);
            var exportProvider = new DisposeTrackingExportProvider(innerExportProvider);

            await exportProvider.DisposeAsync();
            await exportProvider.DisposeAsync();

            Assert.Equal(1, exportProvider.DisposeCallCount);
        }

        [Fact]
        public async Task DisposeAsyncCoreRunsWhenDerivedDisposeOverrideThrows()
        {
            ExportProvider innerExportProvider = await CreateExportProviderAsync(joinableTaskFactory: null);
            var exportProvider = new ThrowingDisposeExportProvider(innerExportProvider);

            await Assert.ThrowsAsync<InvalidOperationException>(() => exportProvider.DisposeAsync().AsTask());

            Assert.True(exportProvider.DisposeAsyncCoreCalled);
        }

        [Fact]
        public async Task DisposeContinuesWithNonSharedDescendantsAfterPartThrows()
        {
            DisposableChildPart.DisposalCount = 0;
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(ThrowingDisposablePartWithChild), typeof(DisposableChildPart));
            exportProvider.GetExportedValue<ThrowingDisposablePartWithChild>();

            Assert.Throws<AggregateException>(() => exportProvider.Dispose());

            Assert.Equal(1, DisposableChildPart.DisposalCount);
        }

        [Fact]
        public async Task DisposeAsyncContinuesWithNonSharedDescendantsAfterPartThrows()
        {
            AsyncDisposableChildPart.DisposalCount = 0;
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(ThrowingAsyncDisposablePartWithChild), typeof(AsyncDisposableChildPart));
            exportProvider.GetExportedValue<ThrowingAsyncDisposablePartWithChild>();

            await Assert.ThrowsAsync<AggregateException>(() => exportProvider.DisposeAsync().AsTask());

            Assert.Equal(1, AsyncDisposableChildPart.DisposalCount);
        }

        [Fact]
        public async Task DisposeAsyncDisposesDescendantWhoseConstructionFinishesLate()
        {
            LateAsyncDisposableChild.Reset();
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(PartWithLateAsyncDisposableChild), typeof(LateAsyncDisposableChild));
            Task<PartWithLateAsyncDisposableChild> activation = Task.Run(exportProvider.GetExportedValue<PartWithLateAsyncDisposableChild>);

            await LateAsyncDisposableChild.ConstructionStarted.Task;
            try
            {
                await exportProvider.DisposeAsync();
            }
            finally
            {
                LateAsyncDisposableChild.AllowConstructionToFinish.SetResult(null);
            }

            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await activation);
            Assert.Equal(1, LateAsyncDisposableChild.DisposalCount);
        }

        [Fact]
        public async Task AsyncDisposablePartDisposedSynchronouslyWithJoinableTaskFactory()
        {
            AsyncDisposablePart.DisposalCount = 0;
            using var joinableTaskContext = new JoinableTaskContext();
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskContext.Factory, typeof(AsyncDisposablePart));
            exportProvider.GetExportedValue<AsyncDisposablePart>();

            exportProvider.Dispose();

            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
        }

        [Fact]
        public async Task AsyncDisposablePartDisposedSynchronouslyWithoutJoinableTaskFactory()
        {
            AsyncDisposablePart.DisposalCount = 0;
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(AsyncDisposablePart));
            exportProvider.GetExportedValue<AsyncDisposablePart>();

            exportProvider.Dispose();

            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
        }

        [Fact]
        public async Task AsyncDisposablePartCreatedByExportFactoryDisposedSynchronouslyWithoutJoinableTaskFactory()
        {
            AsyncDisposablePart.DisposalCount = 0;
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(AsyncDisposablePart), typeof(AsyncDisposablePartFactory));
            AsyncDisposablePartFactory partFactory = exportProvider.GetExportedValue<AsyncDisposablePartFactory>();

            using (partFactory.Factory.CreateExport())
            {
            }

            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
            exportProvider.Dispose();
            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
        }

        [Fact]
        public async Task DualDisposablePartCreatedByExportFactoryUsesIDisposableWithoutJoinableTaskFactory()
        {
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskFactory: null, typeof(DualDisposablePart), typeof(DualDisposablePartFactory));
            DualDisposablePartFactory partFactory = exportProvider.GetExportedValue<DualDisposablePartFactory>();
            var export = partFactory.Factory.CreateExport();
            DualDisposablePart part = export.Value;

            export.Dispose();

            Assert.True(part.DisposeCalled);
            Assert.False(part.DisposeAsyncCalled);
        }

        [Fact]
        public async Task DualDisposablePartCreatedByExportFactoryUsesIAsyncDisposableWithJoinableTaskFactory()
        {
            using var joinableTaskContext = new JoinableTaskContext();
            ExportProvider exportProvider = await CreateExportProviderAsync(joinableTaskContext.Factory, typeof(DualDisposablePart), typeof(DualDisposablePartFactory));
            DualDisposablePartFactory partFactory = exportProvider.GetExportedValue<DualDisposablePartFactory>();
            var export = partFactory.Factory.CreateExport();
            DualDisposablePart part = export.Value;

            export.Dispose();

            Assert.False(part.DisposeCalled);
            Assert.True(part.DisposeAsyncCalled);
        }

        [Fact]
        public async Task AsyncDisposablePartCreatedByMefV1ExportFactoryDisposedSynchronouslyWithJoinableTaskFactory()
        {
            AsyncDisposablePart.DisposalCount = 0;
            using var joinableTaskContext = new JoinableTaskContext();
            ExportProvider exportProvider = await CreateExportProviderAsync(TestUtilities.V1Discovery, joinableTaskContext.Factory, typeof(AsyncDisposablePart), typeof(AsyncDisposablePartFactoryV1));
            AsyncDisposablePartFactoryV1 partFactory = exportProvider.GetExportedValue<AsyncDisposablePartFactoryV1>();

            using (partFactory.Factory.CreateExport())
            {
            }

            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
            exportProvider.Dispose();
            Assert.Equal(1, AsyncDisposablePart.DisposalCount);
        }

        private static async Task<ExportProvider> CreateExportProviderAsync(JoinableTaskFactory? joinableTaskFactory, params Type[] partTypes)
        {
            return await CreateExportProviderAsync(TestUtilities.V2Discovery, joinableTaskFactory, partTypes);
        }

        private static async Task<ExportProvider> CreateExportProviderAsync(PartDiscovery partDiscovery, JoinableTaskFactory? joinableTaskFactory, params Type[] partTypes)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(await partDiscovery.CreatePartsAsync(partTypes));
            CompositionConfiguration configuration = CompositionConfiguration.Create(catalog);
            IExportProviderFactory exportProviderFactory;
            if (joinableTaskFactory is object)
            {
                exportProviderFactory = configuration.CreateExportProviderFactory(joinableTaskFactory);
            }
            else
            {
#pragma warning disable VSTHRD012 // This path tests direct blocking and asynchronous disposal without a joinable task factory.
                exportProviderFactory = configuration.CreateExportProviderFactory();
#pragma warning restore VSTHRD012
            }

            return exportProviderFactory.CreateExportProvider();
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class AsyncDisposablePart : IAsyncDisposable
        {
            internal static int DisposalCount;

            public async ValueTask DisposeAsync()
            {
                await Task.Yield();
                DisposalCount++;
            }
        }

        [Export]
        public class DualDisposablePart : IDisposable, IAsyncDisposable
        {
            internal bool DisposeCalled { get; private set; }

            internal bool DisposeAsyncCalled { get; private set; }

            public void Dispose()
            {
                this.DisposeCalled = true;
            }

            public ValueTask DisposeAsync()
            {
                this.DisposeAsyncCalled = true;
                return default;
            }
        }

        [Export]
        public class AsyncDisposalGatePart : IAsyncDisposable
        {
            internal TaskCompletionSource<object?> AllowDisposalToComplete { get; } = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal TaskCompletionSource<object?> DisposalStarted { get; } = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal int DisposalCount { get; private set; }

            public async ValueTask DisposeAsync()
            {
                this.DisposalCount++;
                this.DisposalStarted.SetResult(null);
#pragma warning disable VSTHRD003 // This test part intentionally awaits a signal controlled by the test.
                await this.AllowDisposalToComplete.Task;
#pragma warning restore VSTHRD003
            }
        }

        [Export]
        public class ThrowingDisposablePartWithChild : IDisposable
        {
            [Import]
            public DisposableChildPart Child { get; set; } = null!;

            public void Dispose()
            {
                throw new InvalidOperationException();
            }
        }

        [Export]
        public class DisposableChildPart : IDisposable
        {
            internal static int DisposalCount;

            public void Dispose()
            {
                DisposalCount++;
            }
        }

        [Export]
        public class ThrowingAsyncDisposablePartWithChild : IAsyncDisposable
        {
            [Import]
            public AsyncDisposableChildPart Child { get; set; } = null!;

            public ValueTask DisposeAsync()
            {
                throw new InvalidOperationException();
            }
        }

        [Export]
        public class AsyncDisposableChildPart : IAsyncDisposable
        {
            internal static int DisposalCount;

            public ValueTask DisposeAsync()
            {
                DisposalCount++;
                return default;
            }
        }

        [Export]
        public class PartWithLateAsyncDisposableChild
        {
            [Import]
            public LateAsyncDisposableChild Child { get; set; } = null!;
        }

        [Export]
        public class LateAsyncDisposableChild : IAsyncDisposable
        {
            internal static TaskCompletionSource<object?> AllowConstructionToFinish = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal static TaskCompletionSource<object?> ConstructionStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal static int DisposalCount;

            public LateAsyncDisposableChild()
            {
                ConstructionStarted.SetResult(null);
#pragma warning disable VSTHRD002 // The test intentionally holds construction across provider disposal.
                AllowConstructionToFinish.Task.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }

            public ValueTask DisposeAsync()
            {
                DisposalCount++;
                return default;
            }

            internal static void Reset()
            {
                AllowConstructionToFinish = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                ConstructionStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                DisposalCount = 0;
            }
        }

        private class DisposeTrackingExportProvider : DelegatingExportProvider
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="DisposeTrackingExportProvider"/> class.
            /// </summary>
            /// <param name="inner">The export provider to delegate to.</param>
            internal DisposeTrackingExportProvider(ExportProvider inner)
                : base(inner)
            {
            }

            /// <summary>
            /// Gets the number of calls to <see cref="Dispose(bool)"/>.
            /// </summary>
            internal int DisposeCallCount { get; private set; }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                this.DisposeCallCount++;
                base.Dispose(disposing);
            }
        }

        private class PartOwningDelegatingExportProvider : DelegatingExportProvider
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PartOwningDelegatingExportProvider"/> class.
            /// </summary>
            /// <param name="inner">The export provider to delegate to.</param>
            /// <param name="part">The part whose disposal should be tracked.</param>
            internal PartOwningDelegatingExportProvider(ExportProvider inner, IDisposable part)
                : base(inner)
            {
                this.TrackDisposableValue(part, sharingBoundary: null);
            }

            /// <summary>
            /// Tracks another part for disposal.
            /// </summary>
            /// <param name="part">The part whose disposal should be tracked.</param>
            internal void TrackPart(IDisposable part)
            {
                this.TrackDisposableValue(part, sharingBoundary: null);
            }
        }

        private class ThrowingDisposeExportProvider : DelegatingExportProvider
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ThrowingDisposeExportProvider"/> class.
            /// </summary>
            /// <param name="inner">The export provider to delegate to.</param>
            internal ThrowingDisposeExportProvider(ExportProvider inner)
                : base(inner)
            {
            }

            /// <summary>
            /// Gets a value indicating whether <see cref="DisposeAsyncCore"/> was invoked.
            /// </summary>
            internal bool DisposeAsyncCoreCalled { get; private set; }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                throw new InvalidOperationException();
            }

            /// <inheritdoc />
            protected override async ValueTask DisposeAsyncCore()
            {
                this.DisposeAsyncCoreCalled = true;
                await base.DisposeAsyncCore();
            }
        }

        [Export]
        public class AsyncDisposablePartFactory
        {
            [Import]
            public ExportFactory<AsyncDisposablePart> Factory { get; set; } = null!;
        }

        [Export]
        public class DualDisposablePartFactory
        {
            [Import]
            public ExportFactory<DualDisposablePart> Factory { get; set; } = null!;
        }

        [MefV1.Export]
        public class AsyncDisposablePartFactoryV1
        {
            [MefV1.Import]
            public MefV1.ExportFactory<AsyncDisposablePart> Factory { get; set; } = null!;
        }

        #region Disposable part happy path test

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableNonSharedPart), typeof(UninstantiatedNonSharedPart))]
        public void DisposableNonSharedPartDisposedWithContainerAfterDirectAcquisition(IContainer container)
        {
            var part = container.GetExportedValue<DisposableNonSharedPart>();
            Assert.False(part.IsDisposed);
            container.Dispose();
            Assert.True(part.IsDisposed);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableNonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void DisposableNonSharedPartDisposedWithContainerForAllInstancesAndThenReleased(IContainer container)
        {
            // The allocations have to happen in another method so that any references held by locals
            // that the compiler creates and we can't directly clear are definitely released.
            var weakRefs = DisposableNonSharedPartDisposedWithContainerForAllInstancesAndThenReleased_Helper(container);
            GC.Collect();
            Assert.True(weakRefs.All(r => !r.IsAlive));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] DisposableNonSharedPartDisposedWithContainerForAllInstancesAndThenReleased_Helper(IContainer container)
        {
            var weakRefs = new WeakReference[3];
            var parts = new DisposableNonSharedPart[weakRefs.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = container.GetExportedValue<DisposableNonSharedPart>();
            }

            Assert.True(parts.All(p => !p.IsDisposed));
            container.Dispose();
            Assert.True(parts.All(p => p.IsDisposed));

            // Verify that the container is not holding references any more.
            for (int i = 0; i < parts.Length; i++)
            {
                weakRefs[i] = new WeakReference(parts[i]);
            }

            return weakRefs;
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableSharedPart))]
        public void DisposableSharedPartDisposedWithContainerAfterDirectAcquisition(IContainer container)
        {
            var part = container.GetExportedValue<DisposableSharedPart>();
            Assert.False(part.IsDisposed);
            container.Dispose();
            Assert.True(part.IsDisposed);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(CachedSharedPart), typeof(ThrowingDisposableSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void DisposedProviderReleasesCachedSharedValueWhenPartDisposalThrows(IContainer container)
        {
            WeakReference cachedPart = DisposedProviderReleasesCachedSharedValueWhenPartDisposalThrowsHelper(container);
            GC.Collect();
            Assert.False(cachedPart.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference DisposedProviderReleasesCachedSharedValueWhenPartDisposalThrowsHelper(IContainer container)
        {
            var cachedPart = container.GetExportedValue<CachedSharedPart>();
            _ = container.GetExportedValue<ThrowingDisposableSharedPart>();
            var weakReference = new WeakReference(cachedPart);
            Assert.Throws<AggregateException>(() => container.Dispose());
            return weakReference;
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableNonSharedPart), typeof(UninstantiatedNonSharedPart), typeof(NonSharedPartThatImportsDisposableNonSharedPart))]
        public void DisposableNonSharedPartDisposedWithContainerAfterImportToANonSharedPart(IContainer container)
        {
            var part = container.GetExportedValue<NonSharedPartThatImportsDisposableNonSharedPart>();
            Assert.False(part.ImportOfDisposableNonSharedPart.IsDisposed);
            container.Dispose();
            Assert.True(part.ImportOfDisposableNonSharedPart.IsDisposed);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableNonSharedPart), typeof(UninstantiatedNonSharedPart), typeof(SharedPartThatImportsDisposableNonSharedPart))]
        public void DisposableNonSharedPartDisposedWithContainerAfterImportToASharedPart(IContainer container)
        {
            var part = container.GetExportedValue<SharedPartThatImportsDisposableNonSharedPart>();
            Assert.False(part.ImportOfDisposableNonSharedPart.IsDisposed);
            container.Dispose();
            Assert.True(part.ImportOfDisposableNonSharedPart.IsDisposed);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithExportingPropertyForDisposableNonSharedPart), typeof(NonSharedPartThatImportsDisposableNonSharedPart))]
        public void DisposableNonSharedPartExportedViaPropertyIsNotDisposedWithContainer(IContainer container)
        {
            var part = container.GetExportedValue<NonSharedPartThatImportsDisposableNonSharedPart>();
            var exportDirect = container.GetExportedValue<DisposableNonSharedPart>();
            Assert.False(part.ImportOfDisposableNonSharedPart.IsDisposed);
            Assert.False(exportDirect.IsDisposed);
            container.Dispose();
            Assert.False(part.ImportOfDisposableNonSharedPart.IsDisposed);
            Assert.False(exportDirect.IsDisposed);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPartThatImportsDisposableNonSharedPart
        {
            [Import, MefV1.Import]
            public DisposableNonSharedPart ImportOfDisposableNonSharedPart { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class SharedPartThatImportsDisposableNonSharedPart
        {
            [Import, MefV1.Import]
            public DisposableNonSharedPart ImportOfDisposableNonSharedPart { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class DisposableNonSharedPart : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class DisposableSharedPart : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }

        [Export, Shared]
        public class CachedSharedPart : IDisposable
        {
            public void Dispose()
            {
            }
        }

        [Export, Shared]
        public class ThrowingDisposableSharedPart : IDisposable
        {
            public void Dispose()
            {
                throw new InvalidOperationException();
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class UninstantiatedNonSharedPart : IDisposable
        {
            public UninstantiatedNonSharedPart()
            {
                Assert.Fail("This should never be instantiated.");
            }

            public void Dispose()
            {
            }
        }

        public class PartWithExportingPropertyForDisposableNonSharedPart
        {
            [Export, MefV1.Export]
            public DisposableNonSharedPart ExportingProperty => new DisposableNonSharedPart();
        }

        #endregion

        #region Part disposed on exception test

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ThrowingPart), typeof(ImportToThrowingPart))]
        public void PartDisposedWhenThrows(IContainer container)
        {
            ThrowingPart.InstantiatedCounter = 0;
            ThrowingPart.DisposedCounter = 0;

            // We don't use Assert.Throws<T> for this next bit because the containers vary in what
            // exception type they throw, and this test isn't about verifying which exception is thrown.
            try
            {
                container.GetExportedValue<ThrowingPart>();
                Assert.Fail("An exception should have been thrown.");
            }
            catch { }

            Assert.Equal(1, ThrowingPart.InstantiatedCounter);
            Assert.Equal(0, ThrowingPart.DisposedCounter);

            container.Dispose();
            Assert.Equal(1, ThrowingPart.InstantiatedCounter);
            Assert.Equal(1, ThrowingPart.DisposedCounter);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ThrowingPart : IDisposable
        {
            internal static int InstantiatedCounter;
            internal static int DisposedCounter;

            public ThrowingPart()
            {
                InstantiatedCounter++;
            }

            [Import]
            [MefV1.Import]
            public ImportToThrowingPart ImportProperty
            {
                set { throw new ApplicationException(); }
            }

            public void Dispose()
            {
                DisposedCounter++;
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ImportToThrowingPart
        {
        }

        #endregion

        #region Internal Disposable part test

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(InternalDisposablePart))]
        public void InternalDisposablePartDisposedWithContainer(IContainer container)
        {
            var part = container.GetExportedValue<InternalDisposablePart>();
            Assert.False(part.IsDisposed);
            container.Dispose();
            Assert.True(part.IsDisposed);
        }

        [MefV1.Export]
        internal class InternalDisposablePart : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }

        #endregion

        #region Part throws in Dispose

        /// <summary>
        /// This test documents MEFv1/v2 behavior that disposal of a container is aborted
        /// when a part's Dispose method throws.
        /// </summary>
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, typeof(DisposeOrderTracker), typeof(PartThrowsInDispose), typeof(NoThrow1), NoCompatGoal = true)]
        public void DisposeContainerAbortsWhenPartDisposeThrowsV1V2(IContainer container)
        {
            var throwingPart = container.GetExportedValue<PartThrowsInDispose>();
            var nonThrowingPart = container.GetExportedValue<NoThrow1>();
            var tracker = container.GetExportedValue<DisposeOrderTracker>();

            // For the verification to be valid, we need to verify that disposal continues *after*
            // the part that throws is disposed of. Since order of part disposal is undefined,
            // this test may be a bit fragile to product changes and may need to be touched up
            // periodically to try to get the throwing part to be disposed of before some other part.
            try
            {
                container.Dispose();
            }
            catch (InvalidOperationException)
            {
            }

            Assert.False(tracker.WasPartDisposedAfterThrowingPartDisposed);
        }

        /// <summary>
        /// Verifies that MEFv3 disposes all parts even if some of them throw.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(DisposeOrderTracker), typeof(PartThrowsInDispose), typeof(NoThrow1))]
        public void DisposeContainerDisposesAllPartsEvenIfTheyThrowV3(IContainer container)
        {
            var throwingPart = container.GetExportedValue<PartThrowsInDispose>();
            var nonThrowingPart = container.GetExportedValue<NoThrow1>();
            var tracker = container.GetExportedValue<DisposeOrderTracker>();

            // For the verification to be valid, we need to verify that disposal continues *after*
            // the part that throws is disposed of. Since order of part disposal is undefined,
            // this test may be a bit fragile to product changes and may need to be touched up
            // periodically to try to get the throwing part to be disposed of before some other part.
            try
            {
                container.Dispose();
            }
            catch (AggregateException ex)
            {
                // We accept that MEF will allow the exception to propagate to us.
                // We just want it to have finished the job first.
                Assert.IsType<InvalidOperationException>(ex.InnerException);
            }

            Assert.True(tracker.WasPartDisposedAfterThrowingPartDisposed);
        }

        [Export, Shared, MefV1.Export]
        public class DisposeOrderTracker
        {
            private bool throwingPartDisposed;

            public bool WasPartDisposedAfterThrowingPartDisposed { get; private set; }

            public void ReportThrowingPartDisposed()
            {
                this.throwingPartDisposed = true;
            }

            public void ReportNonThrowingPartDisposed()
            {
                // Only set to true if the throwing part was already disposed of
                // since we're trying to test exactly that case.
                this.WasPartDisposedAfterThrowingPartDisposed |= this.throwingPartDisposed;
            }
        }

        [Export, MefV1.Export]
        public class PartThrowsInDispose : IDisposable
        {
            [Import, MefV1.Import]
            public DisposeOrderTracker Tracker { get; set; } = null!;

            public void Dispose()
            {
                this.Tracker.ReportThrowingPartDisposed();
                throw new InvalidOperationException("oops");
            }
        }

        [Export, MefV1.Export]
        public class NoThrow1 : IDisposable
        {
            [Import, MefV1.Import]
            public DisposeOrderTracker Tracker { get; set; } = null!;

            public void Dispose()
            {
                this.Tracker.ReportNonThrowingPartDisposed();
            }
        }

        #endregion

        [MefFact(CompositionEngines.V1Compat, new Type[0])]
        public void ContainerThrowsAfterDisposal(IContainer container)
        {
            container.Dispose();
            Assert.Throws<ObjectDisposedException>(() => container.GetExport<string>());
            Assert.Throws<ObjectDisposedException>(() => container.GetExport<string>(null));
            Assert.Throws<ObjectDisposedException>(() => container.GetExport<string, IDictionary<string, object>>());
            Assert.Throws<ObjectDisposedException>(() => container.GetExport<string, IDictionary<string, object>>(null));
            Assert.Throws<ObjectDisposedException>(() => container.GetExportedValue<string>());
            Assert.Throws<ObjectDisposedException>(() => container.GetExportedValue<string>(null));
            Assert.Throws<ObjectDisposedException>(() => container.GetExportedValues<string>());
            Assert.Throws<ObjectDisposedException>(() => container.GetExportedValues<string>(null));
            Assert.Throws<ObjectDisposedException>(() => container.GetExports<string>());
            Assert.Throws<ObjectDisposedException>(() => container.GetExports<string>(null));
            Assert.Throws<ObjectDisposedException>(() => container.GetExports<string, IDictionary<string, object>>());
            Assert.Throws<ObjectDisposedException>(() => container.GetExports<string, IDictionary<string, object>>(null));
            container.Dispose();
            container.ToString();
            container.GetHashCode();
        }

        #region Disposal evaluates Lazy import which then tries to import the disposed part

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartWithDisposeThatEvaluatesLazyImport), typeof(PartThatImportsDisposeWithLazyImport))]
        public void DisposeEvaluatesLazyImportThatLoopsBackV1(IContainer container)
        {
            var value = container.GetExportedValue<PartWithDisposeThatEvaluatesLazyImport>();
            try
            {
                container.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // MEFv1 and MEFv2 throw this
            }
            catch (AggregateException ex)
            {
                // MEFv3 throws this.
                ex.Handle(e => e is ObjectDisposedException);
            }
        }

        [MefFact(CompositionEngines.V2, typeof(PartWithDisposeThatEvaluatesLazyImport), typeof(PartThatImportsDisposeWithLazyImport), NoCompatGoal = true)]
        public void DisposeEvaluatesLazyImportThatLoopsBackV2(IContainer container)
        {
            var value = container.GetExportedValue<PartWithDisposeThatEvaluatesLazyImport>();
            container.Dispose();
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithDisposeThatEvaluatesLazyImport : IDisposable
        {
            [Import, MefV1.Import]
            public Lazy<PartThatImportsDisposeWithLazyImport> LazyImport { get; set; } = null!;

            public void Dispose()
            {
                var other = this.LazyImport.Value;

                // Although we may expect the above line to throw, if it didn't (like in V2)
                // we assert that the follow should be true.
                Assert.Same(this, other.ImportingProperty);
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsDisposeWithLazyImport
        {
            [Import, MefV1.Import]
            public PartWithDisposeThatEvaluatesLazyImport ImportingProperty { get; set; } = null!;
        }

        #endregion

        #region Disposal of sharing boundary part evaluates Lazy import which then tries to import the disposed part

        [MefFact(CompositionEngines.V2, typeof(SharingBoundaryFactory), typeof(SharingBoundaryPartWithDisposeThatEvaluatesLazyImport), typeof(PartThatImportsSharingBoundaryDisposeWithLazyImport), NoCompatGoal = true)]
        public void DisposeOfSharingBoundaryPartEvaluatesLazyImportThatLoopsBackV2(IContainer container)
        {
            var factory = container.GetExportedValue<SharingBoundaryFactory>();
            var export = factory.Factory.CreateExport();
            export.Dispose();
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(SharingBoundaryFactory), typeof(SharingBoundaryPartWithDisposeThatEvaluatesLazyImport), typeof(PartThatImportsSharingBoundaryDisposeWithLazyImport))]
        public void DisposeOfSharingBoundaryPartEvaluatesLazyImportThatLoopsBackV3(IContainer container)
        {
            var factory = container.GetExportedValue<SharingBoundaryFactory>();
            var export = factory.Factory.CreateExport();
            try
            {
                export.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // MEFv1 and MEFv2 throw this
            }
            catch (AggregateException ex)
            {
                // MEFv3 throws this.
                ex.Handle(e => e is ObjectDisposedException);
            }
        }

        [Export, Shared]
        public class SharingBoundaryFactory
        {
            [Import, SharingBoundary("A")]
            public ExportFactory<SharingBoundaryPartWithDisposeThatEvaluatesLazyImport> Factory { get; set; } = null!;
        }

        [Export, Shared("A")]
        public class SharingBoundaryPartWithDisposeThatEvaluatesLazyImport : IDisposable
        {
            [Import]
            public Lazy<PartThatImportsSharingBoundaryDisposeWithLazyImport> LazyImport { get; set; } = null!;

            public void Dispose()
            {
                var other = this.LazyImport.Value;

                // Although we may expect the above line to throw, if it didn't (like in V2)
                // we assert that the follow should be true.
                Assert.Same(this, other.ImportedArgument);
            }
        }

        [Export, Shared("A")]
        public class PartThatImportsSharingBoundaryDisposeWithLazyImport
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PartThatImportsSharingBoundaryDisposeWithLazyImport"/> class.
            /// </summary>
            /// <devremarks>
            /// This is deliberately an importing constructor rather than an importing property
            /// so as to exercise the code path that was misbehaving when we wrote the test.
            /// </devremarks>
            [ImportingConstructor]
            public PartThatImportsSharingBoundaryDisposeWithLazyImport(SharingBoundaryPartWithDisposeThatEvaluatesLazyImport importingArg)
            {
                this.ImportedArgument = importingArg;
            }

            public SharingBoundaryPartWithDisposeThatEvaluatesLazyImport ImportedArgument { get; set; }
        }

        #endregion
    }
}
