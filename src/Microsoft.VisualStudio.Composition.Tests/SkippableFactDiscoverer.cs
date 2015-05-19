namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class SkippableFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink diagnosticMessageSink;

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

        internal class SkippableFactCommand : XunitTestCase
        {
            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete("Called by the de-serializer", true)]
            public SkippableFactCommand() { }

            public SkippableFactCommand(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, object[] testMethodArguments = null)
                : base(diagnosticMessageSink, defaultMethodDisplay, testMethod, testMethodArguments)
            {
            }

            public SkippableFactCommand(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, string skipReason)
                : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
            {
                this.SkipReason = skipReason;
            }

            public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                var messageBusInterceptor = new MessageBusInterceptor(messageBus);
                var result = await base.RunAsync(diagnosticMessageSink, messageBusInterceptor, constructorArguments, aggregator, cancellationTokenSource);
                result.Failed -= messageBusInterceptor.SkippedCount;
                result.Skipped += messageBusInterceptor.SkippedCount;
                return result;
            }

            private class MessageBusInterceptor : IMessageBus
            {
                private readonly IMessageBus inner;

                internal MessageBusInterceptor(IMessageBus inner)
                {
                    this.inner = inner;
                }

                internal int SkippedCount { get; private set; }

                public void Dispose()
                {
                    this.inner.Dispose();
                }

                public bool QueueMessage(IMessageSinkMessage message)
                {
                    var failed = message as TestFailed;
                    if (failed != null)
                    {
                        if (failed.ExceptionTypes.Length == 1 && failed.ExceptionTypes[0] == typeof(SkipException).FullName)
                        {
                            this.SkippedCount++;
                            return this.inner.QueueMessage(new TestSkipped(failed.Test, failed.Messages[0]));
                        }
                    }

                    return this.inner.QueueMessage(message);
                }
            }
        }
    }
}
