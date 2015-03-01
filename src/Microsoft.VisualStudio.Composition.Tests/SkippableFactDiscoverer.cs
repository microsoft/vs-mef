namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class SkippableFactDiscoverer : IXunitTestCaseDiscoverer
    {
        readonly IMessageSink diagnosticMessageSink;

        /// <summary> 
        /// Initializes a new instance of the <see cref="SkippableFactDiscoverer"/> class. 
        /// </summary> 
        /// <param name="diagnosticMessageSink">The message sink used to send diagnostic messages</param> 
        public SkippableFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            yield return new SkippableFactCommand(this.diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod);
        }

        /// <summary>
        /// The exception to throw to register a skipped test.
        /// </summary>
        public class SkipException : Exception
        {
            public SkipException(string reason) : base(reason) { }
        }

        private class SkippableFactCommand : XunitTestCase
        {
            public SkippableFactCommand(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, object[] testMethodArguments = null)
                : base(diagnosticMessageSink, defaultMethodDisplay, testMethod, testMethodArguments)
            {
            }

            public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    return await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                }
                catch (SkipException)
                {
                    return new RunSummary { Skipped = 1, Total = 1, Time = (decimal)timer.Elapsed.TotalSeconds };
                }
            }
        }
    }
}
