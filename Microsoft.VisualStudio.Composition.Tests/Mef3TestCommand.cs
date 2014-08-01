namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using Xunit;
    using Xunit.Sdk;

    public class Mef3TestCommand : FactCommand
    {
        private readonly ComposableCatalog catalog;
        private readonly bool expectInvalidConfiguration;
        private readonly CompositionEngines compositionVersions;
        private readonly bool runtime;

        public Mef3TestCommand(IMethodInfo method, ComposableCatalog catalog, CompositionEngines compositionVersions, bool expectInvalidConfiguration, bool runtime)
            : base(method)
        {
            Requires.NotNull(catalog, "catalog");
            this.catalog = catalog;
            this.compositionVersions = compositionVersions;
            this.expectInvalidConfiguration = expectInvalidConfiguration;
            this.runtime = runtime;

            this.DisplayName = string.Format("V3 engine ({0})", runtime ? "runtime" : "code gen");
        }

        public override MethodResult Execute(object testClass)
        {
            var catalog = this.catalog
                .WithCompositionService()
                .WithDesktopSupport();
            var configuration = CompositionConfiguration.Create(catalog);

            if (!this.compositionVersions.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors))
            {
                Assert.Equal(this.expectInvalidConfiguration, !configuration.CompositionErrors.IsEmpty);
            }

            if (!this.expectInvalidConfiguration)
            {
                var exportProvider = TestUtilities.CreateContainer(configuration, this.runtime);
                var containerWrapper = new TestUtilities.V3ContainerWrapper(exportProvider, configuration);
                this.testMethod.Invoke(testClass, containerWrapper);
            }

            return new PassedResult(this.testMethod, this.DisplayName);
        }
    }
}
