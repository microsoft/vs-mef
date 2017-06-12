// Copyright (c) Microsoft. All rights reserved.

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

#if DESKTOP
    [Serializable]
#endif
    public class LegacyMefTestCaseRunner : XunitTestCaseRunner
    {
        private readonly CompositionEngines engineVersion;
        private readonly Type[] parts;
        private readonly IReadOnlyList<string> assemblies;
        private readonly bool invalidConfiguration;

        public LegacyMefTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, CompositionEngines engineVersion, Type[] parts, IReadOnlyList<string> assemblies, bool invalidConfiguration)
            : base(testCase, displayName, skipReason, constructorArguments, null, new TestResultInverter(messageBus, invalidConfiguration), aggregator, cancellationTokenSource)
        {
            Requires.Argument(parts != null || assemblies != null, "parts ?? assemblies", "Either parameter must be non-null.");

            this.engineVersion = engineVersion;
            this.parts = parts;
            this.assemblies = assemblies;
            this.DisplayName = engineVersion.ToString();
            this.invalidConfiguration = invalidConfiguration;
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            var runSummary = await this.RunMultiEngineTestAsync(
                this.engineVersion,
                this.parts,
                this.assemblies,
                async container =>
                {
                    this.TestMethodArguments = new object[] { container };
                    return await base.RunTestAsync();
                });

            var inverter = (TestResultInverter)this.MessageBus;
            runSummary.Failed -= inverter.InvertedFailures;
            runSummary.Failed += inverter.InvertedSuccesses;

            return runSummary;
        }

        private async Task<RunSummary> RunMultiEngineTestAsync(CompositionEngines attributesVersion, Type[] parts, IReadOnlyList<string> assemblies, Func<IContainer, Task<RunSummary>> test)
        {
            try
            {
                parts = parts ?? new Type[0];
                var loadedAssemblies = assemblies != null ? assemblies.Select(an => Assembly.Load(new AssemblyName(an))).ToImmutableList() : ImmutableList<Assembly>.Empty;

                if (attributesVersion.HasFlag(CompositionEngines.V1))
                {
#if DESKTOP
                    return await test(TestUtilities.CreateContainerV1(loadedAssemblies, parts));
#else
                    var t = new XunitTest(this.TestCase, this.DisplayName);
                    if (!this.MessageBus.QueueMessage(new TestSkipped(t, ".NET MEF is not available on .NET Core")))
                    {
                        this.CancellationTokenSource.Cancel();
                    }

                    return new RunSummary { Total = 1, Skipped = 1 };
#endif
                }

                if (attributesVersion.HasFlag(CompositionEngines.V2))
                {
                    return await test(TestUtilities.CreateContainerV2(loadedAssemblies, parts));
                }

                throw new InvalidOperationException();
            }
            catch (Exception ex)
            {
                var t = new XunitTest(this.TestCase, this.DisplayName);
                if (!this.MessageBus.QueueMessage(new TestFailed(t, 0, null, ex)))
                {
                    this.CancellationTokenSource.Cancel();
                }

                return new RunSummary { Total = 1, Failed = 1 };
            }
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

            public int InvertedFailures { get; private set; }

            public int InvertedSuccesses { get; private set; }

            public bool QueueMessage(IMessageSinkMessage message)
            {
                if (this.invalidConfigurationExpected)
                {
                    if (message is TestFailed)
                    {
                        // TODO: allow for derived types of allowed exceptions
                        var failedMessage = (TestFailed)message;
                        if (failedMessage.ExceptionTypes.Length > 0 &&
                            AllowedFailureExceptionTypes.Any(t => t.FullName == failedMessage.ExceptionTypes[0]))
                        {
                            message = new TestPassed(failedMessage.Test, failedMessage.ExecutionTime, failedMessage.Output);
                            this.InvertedFailures++;
                        }
                    }
                    else if (message is TestPassed)
                    {
                        var passedMessage = (TestPassed)message;
                        message = new TestFailed(passedMessage.Test, passedMessage.ExecutionTime, passedMessage.Output, new AssertActualExpectedException(false, true, "Expected invalid configuration but no exception thrown."));
                        this.InvertedSuccesses++;
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
