// Copyright (c) Microsoft. All rights reserved.

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

    public class Mef3TestCaseRunner : XunitTestCaseRunner
    {
        private readonly CompositionConfiguration configuration;
        private readonly CompositionEngines compositionVersions;

        public Mef3TestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, CompositionConfiguration configuration, CompositionEngines compositionVersions)
            : base(testCase, displayName, skipReason, constructorArguments, null, messageBus, aggregator, cancellationTokenSource)
        {
            Requires.NotNull(configuration, nameof(configuration));

            this.configuration = configuration;
            this.compositionVersions = compositionVersions;
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            var output = new TestOutputHelper();
            output.Initialize(this.MessageBus, new XunitTest(this.TestCase, this.DisplayName));
            var exportProvider = await TestUtilities.CreateContainerAsync(this.configuration, output);
            var containerWrapper = new TestUtilities.V3ContainerWrapper(exportProvider, this.configuration);
            this.TestMethodArguments = new object[] { containerWrapper };
            return await base.RunTestAsync();
        }
    }
}
