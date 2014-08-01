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
        private readonly CompositionConfiguration configuration;
        private readonly CompositionEngines compositionVersions;
        private readonly bool runtime;

        public Mef3TestCommand(IMethodInfo method, CompositionConfiguration configuration, CompositionEngines compositionVersions, bool runtime)
            : base(method)
        {
            Requires.NotNull(configuration, "configuration");

            this.configuration = configuration;
            this.compositionVersions = compositionVersions;
            this.runtime = runtime;

            this.DisplayName = string.Format("V3 engine ({0})", runtime ? "runtime" : "code gen");
        }

        public override MethodResult Execute(object testClass)
        {
            var exportProvider = TestUtilities.CreateContainer(this.configuration, this.runtime);
            var containerWrapper = new TestUtilities.V3ContainerWrapper(exportProvider, this.configuration);
            this.testMethod.Invoke(testClass, containerWrapper);

            return new PassedResult(this.testMethod, this.DisplayName);
        }
    }
}
