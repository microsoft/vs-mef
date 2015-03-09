namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class Mef3TestCommand : XunitTestCaseRunner
    {
        private readonly CompositionConfiguration configuration;
        private readonly CompositionEngines compositionVersions;
        private readonly bool runtime;

        public Mef3TestCommand(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, CompositionConfiguration configuration, CompositionEngines compositionVersions, bool runtime)
            : base(testCase, displayName, skipReason, constructorArguments, null, messageBus, aggregator, cancellationTokenSource)
        {
            Requires.NotNull(configuration, "configuration");

            this.configuration = configuration;
            this.compositionVersions = compositionVersions;
            this.runtime = runtime;
        }

        protected override Task<RunSummary> RunTestAsync()
        {
            var output = new TestOutputHelper();
            output.Initialize(this.MessageBus, new XunitTest(this.TestCase, this.DisplayName));
            var exportProvider = TestUtilities.CreateContainer(this.configuration, this.runtime, output);
            var containerWrapper = new TestUtilities.V3ContainerWrapper(exportProvider, this.configuration);
            this.TestMethodArguments = new object[] { containerWrapper };
            return base.RunTestAsync();
        }
    }
}
