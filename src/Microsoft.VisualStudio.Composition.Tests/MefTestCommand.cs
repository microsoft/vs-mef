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
    public class MefTestCommand : XunitTestCase
    {
        private readonly CompositionEngines engineVersion;
        private readonly Type[] parts;
        private readonly IReadOnlyList<string> assemblies;
        private readonly bool invalidConfiguration;

        public MefTestCommand(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, CompositionEngines engineVersion, Type[] parts, IReadOnlyList<string> assemblies, bool invalidConfiguration)
            : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
        {
            Requires.Argument(parts != null || assemblies != null, "parts ?? assemblies", "Either parameter must be non-null.");

            this.engineVersion = engineVersion;
            this.parts = parts;
            this.assemblies = assemblies;
            this.DisplayName = engineVersion.ToString();
            this.invalidConfiguration = invalidConfiguration;
        }

        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            RunSummary runSummary;

            if (this.invalidConfiguration)
            {
                bool compositionExceptionThrown;
                try
                {
                    runSummary = await RunMultiEngineTest(
                        this.engineVersion,
                        this.parts,
                        this.assemblies,
                        async container =>
                        {
                            this.TestMethodArguments = new object[] { container };
                            return await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                        });

                    compositionExceptionThrown = false;
                }
                catch (CompositionFailedException)
                {
                    compositionExceptionThrown = true;
                }
                catch (System.ComponentModel.Composition.CompositionException)
                {
                    compositionExceptionThrown = true;
                }
                catch (InvalidOperationException)
                {
                    // MEFv1 throws this sometimes (CustomMetadataValueV1 test).
                    compositionExceptionThrown = true;
                }
                catch (System.Composition.Hosting.CompositionFailedException)
                {
                    // MEFv2 can throw this from ExportFactory`1.CreateExport.
                    compositionExceptionThrown = true;
                }

                Assert.True(compositionExceptionThrown, "Composition exception expected but not thrown.");
                runSummary = null; // TODO
            }
            else
            {
                runSummary = await RunMultiEngineTest(
                    this.engineVersion,
                    this.parts,
                    this.assemblies,
                    async container =>
                    {
                        this.TestMethodArguments = new object[] { container };
                        return await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                    });
            }

            return runSummary;
        }

        private static Task<RunSummary> RunMultiEngineTest(CompositionEngines attributesVersion, Type[] parts, IReadOnlyList<string> assemblies, Func<IContainer, Task<RunSummary>> test)
        {
            parts = parts ?? new Type[0];
            var loadedAssemblies = assemblies != null ? assemblies.Select(Assembly.Load).ToImmutableList() : ImmutableList<Assembly>.Empty;
            return TestUtilities.RunMultiEngineTest(attributesVersion, loadedAssemblies, parts, test);
        }
    }
}
