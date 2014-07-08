namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class DisposablePartsTests
    {
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
        public void DisposableNonSharedPartDisposedWithContainerForAllInstancesAndThenReleased(IContainer container)
        {
            // The allocations have to happen in another method so that any references held by locals
            // that the compiler creates and we can't directly clear are definitely released.
            var weakRefs = DisposableNonSharedPartDisposedWithContainerForAllInstancesAndThenReleased_Helper(container);
            GC.Collect();
            Assert.True(weakRefs.All(r => !r.IsAlive));
        }

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

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableNonSharedPart), typeof(UninstantiatedNonSharedPart), typeof(NonSharedPartThatImportsDisposableNonSharedPart))]
        public void DisposableNonSharedPartDisposedWithContainerAfterImportToAnotherPart(IContainer container)
        {
            var part = container.GetExportedValue<NonSharedPartThatImportsDisposableNonSharedPart>();
            Assert.False(part.ImportOfDisposableNonSharedPart.IsDisposed);
            container.Dispose();
            Assert.True(part.ImportOfDisposableNonSharedPart.IsDisposed);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPartThatImportsDisposableNonSharedPart
        {
            [Import, MefV1.Import]
            public DisposableNonSharedPart ImportOfDisposableNonSharedPart { get; set; }
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

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class UninstantiatedNonSharedPart : IDisposable
        {
            public UninstantiatedNonSharedPart()
            {
                Assert.False(true, "This should never be instantiated.");
            }

            public void Dispose()
            {
            }
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
                Assert.False(true, "An exception should have been thrown.");
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
    }
}
