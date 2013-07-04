namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class DisposablePartsTests
    {
        [Fact]
        public void DisposablePartDisposedWithContainer()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(DisposablePart));
            var configuration = configurationBuilder.CreateConfiguration();
            var containerFactory = configuration.CreateContainerFactoryAsync().Result;
            var container = containerFactory.CreateContainer();

            var part = container.GetExport<DisposablePart>();
            Assert.False(part.IsDisposed);
            container.Dispose();
            Assert.True(part.IsDisposed);
        }

        [Export]
        public class DisposablePart : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }
    }
}
