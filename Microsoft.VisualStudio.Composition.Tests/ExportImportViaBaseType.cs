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
            var container = TestUtilities.CreateContainer(
                typeof(Implementor),
                typeof(Consumer));

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
