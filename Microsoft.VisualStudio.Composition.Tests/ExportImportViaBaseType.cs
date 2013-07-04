namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ExportImportViaBaseType
    {
        [Fact]
        public void ImportViaExportedInterface()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(Implementor));
            configurationBuilder.AddType(typeof(Consumer));
            var configuration = configurationBuilder.CreateConfiguration();
            var container = configuration.CreateContainer();

            Consumer consumer = container.GetExport<Consumer>();
            Assert.NotNull(consumer);
            Assert.NotNull(consumer.Imported);
            Assert.IsAssignableFrom(typeof(Implementor), consumer.Imported);
        }

        public interface ISomeType { }

        [Export(typeof(ISomeType))]
        public class Implementor : ISomeType { }

        [Export]
        public class Consumer
        {
            [Import]
            public ISomeType Imported { get; set; }
        }
    }
}
