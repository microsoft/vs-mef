namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ImportManyTests
    {
        [Fact]
        public void ImportManyWithNone()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(Extendable));
            configurationBuilder.AddType(typeof(ExtensionOne));
            var configuration = configurationBuilder.CreateConfiguration();
            var container = configuration.CreateContainer();

            var extendable = container.GetExport<Extendable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(0, extendable.Extensions.Count);
        }


        [Fact]
        public void ImportManyWithOne()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(Extendable));
            configurationBuilder.AddType(typeof(ExtensionOne));
            var configuration = configurationBuilder.CreateConfiguration();
            var container = configuration.CreateContainer();

            var extendable = container.GetExport<Extendable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(1, extendable.Extensions.Count);
            Assert.IsAssignableFrom(typeof(ExtensionOne), extendable.Extensions[0]);
        }

        [Fact]
        public void ImportManyWithTwo()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(Extendable));
            configurationBuilder.AddType(typeof(ExtensionOne));
            configurationBuilder.AddType(typeof(ExtensionTwo));
            var configuration = configurationBuilder.CreateConfiguration();
            var container = configuration.CreateContainer();

            var extendable = container.GetExport<Extendable>();
            Assert.NotNull(extendable);
            Assert.NotNull(extendable.Extensions);
            Assert.Equal(2, extendable.Extensions.Count);
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionOne>().Count());
            Assert.Equal(1, extendable.Extensions.OfType<ExtensionTwo>().Count());
        }

        public interface IExtension { }

        [Export(typeof(IExtension))]
        public class ExtensionOne : IExtension { }

        [Export(typeof(IExtension))]
        public class ExtensionTwo : IExtension { }

        [Export]
        public class Extendable
        {
            [ImportMany]
            public List<IExtension> Extensions { get; set; }
        }
    }
}
