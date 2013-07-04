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
            // The configuration is invalid, so prevent its creation.
            Assert.Throws<InvalidOperationException>(() => CompositionConfiguration.Create(typeof(RequiredImportMissing)));
        }

        [Fact]
        public void MissingOptionalImport()
        {
            var configuration = CompositionConfiguration.Create(
                typeof(OptionalImportMissing));
            var container = configuration.CreateContainer();

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
