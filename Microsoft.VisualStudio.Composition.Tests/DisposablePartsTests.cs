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
        public void DisposableNonSharedPartDisposedWithContainer(IContainer container)
        {
            var part = container.GetExportedValue<DisposableNonSharedPart>();
            Assert.False(part.IsDisposed);
            container.Dispose();
            Assert.True(part.IsDisposed);
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
