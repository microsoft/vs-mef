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
        [MefFact(CompositionEngines.V2 | CompositionEngines.V1)]
        public void DisposablePartDisposedWithContainer(IContainer container)
        {
            var part = container.GetExport<DisposablePart>();
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
    }
}
