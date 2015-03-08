namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    [Serializable]
    public class MefTestCommand : XunitTestCaseRunner
    {
        private readonly CompositionEngines engineVersion;
        private readonly Type[] parts;
        private readonly IReadOnlyList<string> assemblies;
        private readonly bool invalidConfiguration;

        public MefTestCommand(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, CompositionEngines engineVersion, Type[] parts, IReadOnlyList<string> assemblies, bool invalidConfiguration)
            : base(testCase, displayName, skipReason, constructorArguments, null, new TestResultInverter(messageBus, invalidConfiguration), aggregator, cancellationTokenSource)
        {
            Requires.Argument(parts != null || assemblies != null, "parts ?? assemblies", "Either parameter must be non-null.");

            this.engineVersion = engineVersion;
            this.parts = parts;
            this.assemblies = assemblies;
            this.DisplayName = engineVersion.ToString();
            this.invalidConfiguration = invalidConfiguration;
        }

        protected override Task<RunSummary> RunTestAsync()
        {
            return RunMultiEngineTestAsync(
                this.engineVersion,
                this.parts,
                this.assemblies,
                async container =>
                {
                    this.TestMethodArguments = new object[] { container };
                    return await base.RunTestAsync();
                });
        }

        private static Task<RunSummary> RunMultiEngineTestAsync(CompositionEngines attributesVersion, Type[] parts, IReadOnlyList<string> assemblies, Func<IContainer, Task<RunSummary>> test)
        {
            parts = parts ?? new Type[0];
            var loadedAssemblies = assemblies != null ? assemblies.Select(Assembly.Load).ToImmutableList() : ImmutableList<Assembly>.Empty;

            if (attributesVersion.HasFlag(CompositionEngines.V1))
            {
                return test(TestUtilities.CreateContainerV1(loadedAssemblies, parts));
            }

            if (attributesVersion.HasFlag(CompositionEngines.V2))
            {
                return test(TestUtilities.CreateContainerV2(loadedAssemblies, parts));
            }

            throw new InvalidOperationException();
        }

        private class TestResultInverter : IMessageBus
        {
            private static readonly Type[] AllowedFailureExceptionTypes = new Type[]
            {
                typeof(CompositionFailedException),
                typeof(System.ComponentModel.Composition.CompositionException),
                typeof(InvalidOperationException), // MEFv1 throws this sometimes (CustomMetadataValueV1 test).
                typeof(System.Composition.Hosting.CompositionFailedException), // MEFv2 can throw this from ExportFactory`1.CreateExport.
            };

            private readonly IMessageBus inner;
            private readonly bool invalidConfigurationExpected;

            internal TestResultInverter(IMessageBus inner, bool invalidConfigurationExpected)
            {
                this.inner = inner;
                this.invalidConfigurationExpected = invalidConfigurationExpected;
            }

            public bool QueueMessage(IMessageSinkMessage message)
            {
                if (this.invalidConfigurationExpected)
                {
                    if (message is TestFailed)
                    {
                        var failedMessage = (TestFailed)message;
                        if (failedMessage.ExceptionTypes.Length == 1 &&
                            AllowedFailureExceptionTypes.Any(t => t.FullName == failedMessage.ExceptionTypes[0]))
                        {
                            message = new TestPassed(failedMessage.Test, failedMessage.ExecutionTime, failedMessage.Output);
                        }
                    }
                    else if (message is TestPassed)
                    {
                        var passedMessage = (TestPassed)message;
                        message = new TestFailed(passedMessage.Test, passedMessage.ExecutionTime, passedMessage.Output, new AssertActualExpectedException(false, true, "Expected invalid configuration but no exception thrown."));
                    }
                }

                return this.inner.QueueMessage(message);
            }

            public void Dispose()
            {
                this.inner.Dispose();
            }
        }
    }
}
