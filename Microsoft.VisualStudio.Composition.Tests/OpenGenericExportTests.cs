namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class OpenGenericExportTests
    {
        [Fact]
        public void AcquireOpenGenericExportv2()
        {
            var configuration = new ContainerConfiguration()
                .WithPart(typeof(Useful<>))
                .WithPart(typeof(User));
            CompositionHost container = configuration.CreateContainer();
            Useful<int> useful = container.GetExport<Useful<int>>();
            Assert.NotNull(useful);
        }

        [Fact]
        public void AcquireOpenGenericExportv3()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(User));
            configurationBuilder.AddType(typeof(Useful<>));
            var configuration = configurationBuilder.CreateConfiguration();
            var containerFactory = configuration.CreateContainerFactoryAsync().Result;
            var container = containerFactory.CreateContainer();

            Useful<int> useful = container.GetExport<Useful<int>>();
            Assert.NotNull(useful);
        }

        [Fact]
        public void AcquireExportWithImportOfOpenGenericExportv2()
        {
            var configuration = new ContainerConfiguration()
                .WithPart(typeof(Useful<>))
                .WithPart(typeof(User));
            CompositionHost container = configuration.CreateContainer();
            User user = container.GetExport<User>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
        }

        [Fact]
        public void AcquireExportWithImportOfOpenGenericExportv3()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(User));
            configurationBuilder.AddType(typeof(Useful<>));
            var configuration = configurationBuilder.CreateConfiguration();
            var containerFactory = configuration.CreateContainerFactoryAsync().Result;
            var container = containerFactory.CreateContainer();

            User user = container.GetExport<User>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
        }

        [Export]
        public class Useful<T> { }

        [Export]
        public class User
        {
            [Import]
            public Useful<int> Useful { get; set; }
        }
    }
}
