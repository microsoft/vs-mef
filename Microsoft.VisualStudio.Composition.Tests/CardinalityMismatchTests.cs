namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class CardinalityMismatchTests
    {
        [Fact]
        public void MissingRequiredImport()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(RequiredImportMissing));

            // The configuration is invalid, so prevent its creation.
            Assert.Throws<InvalidOperationException>(() => configurationBuilder.CreateConfiguration());
        }

        [Fact]
        public void MissingOptionalImport()
        {
            var configurationBuilder = new CompositionConfigurationBuilder();
            configurationBuilder.AddType(typeof(OptionalImportMissing));
            var configuration = configurationBuilder.CreateConfiguration();
            var containerFactory = configuration.CreateContainerFactoryAsync().Result;
            var container = containerFactory.CreateContainer();

            var export = container.GetExport<OptionalImportMissing>();
            Assert.NotNull(export);
            Assert.Null(export.MissingOptionalImport);
        }

        [Export]
        public class RequiredImportMissing
        {
            [Import]
            public ICustomFormatter MissingRequiredImport { get; set; }
        }

        [Export]
        public class OptionalImportMissing
        {
            [Import(AllowDefault = true)]
            public ICustomFormatter MissingOptionalImport { get; set; }
        }
    }
}
