namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV3 = System.ComponentModel.Composition;

    public class DisposablePartsTests
    {
        #region Dispoable part happy path test

        [MefFact(CompositionEngines.V2Compat | CompositionEngines.V1Compat, typeof(DisposablePart), typeof(UninstantiatedPart))]
        public void DisposablePartDisposedWithContainer(IContainer container)
        {
            var part = container.GetExportedValue<DisposablePart>();
            Assert.False(part.IsDisposed);
            container.Dispose();
            Assert.True(part.IsDisposed);
        }

        [Export]
        [MefV3.Export, MefV3.PartCreationPolicy(MefV3.CreationPolicy.NonShared)]
        public class DisposablePart : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }

        [Export]
        [MefV3.Export, MefV3.PartCreationPolicy(MefV3.CreationPolicy.NonShared)]
        public class UninstantiatedPart : IDisposable
        {
            public UninstantiatedPart()
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
            try
            {
                container.GetExportedValue<ThrowingPart>();
                Assert.False(true, "An exception should have been thrown.");
            }
            catch { }

            container.Dispose();
            Assert.Equal(1, ThrowingPart.InstantiatedCounter);
            Assert.Equal(1, ThrowingPart.DisposedCounter);
        }

        [Export]
        [MefV3.Export, MefV3.PartCreationPolicy(MefV3.CreationPolicy.NonShared)]
        public class ThrowingPart : IDisposable
        {
            internal static int InstantiatedCounter;
            internal static int DisposedCounter;

            public ThrowingPart()
            {
                InstantiatedCounter++;
            }

            [Import]
            [MefV3.Import]
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
        [MefV3.Export, MefV3.PartCreationPolicy(MefV3.CreationPolicy.NonShared)]
        public class ImportToThrowingPart
        {
        }

        #endregion
    }
}
