// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class MefFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink diagnosticMessageSink;

        /// <summary>
        /// Initializes a new instance of the <see cref="MefFactDiscoverer"/> class.
        /// </summary>
        /// <param name="diagnosticMessageSink">The message sink used to send diagnostic messages</param>
        public MefFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttributeInfo)
        {
            var methodDisplay = discoveryOptions.MethodDisplayOrDefault();

            yield return new MefFactTestCase(this.diagnosticMessageSink, methodDisplay, testMethod, factAttributeInfo);
        }

        private static IEnumerable<Type> GetNestedTypesRecursively(Type parentType)
        {
            Requires.NotNull(parentType, nameof(parentType));

            foreach (var nested in parentType.GetTypeInfo().GetNestedTypes())
            {
                yield return nested;

                foreach (var recursive in GetNestedTypesRecursively(nested))
                {
                    yield return recursive;
                }
            }
        }

        private class MefFactTestCase : XunitTestCase
        {
            private Type[] parts;
            private IReadOnlyList<string> assemblies;
            private CompositionEngines compositionVersions;
            private bool noCompatGoal;
            private bool invalidConfiguration;

            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete("Called by the de-serializer", true)]
            public MefFactTestCase() { }

            public MefFactTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, IAttributeInfo factAttributeInfo)
                : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
            {
                var factAttribute = MefFactAttribute.Instantiate(factAttributeInfo);
                this.SkipReason = factAttribute.Skip;
                this.parts = factAttribute.Parts;
                this.assemblies = factAttribute.Assemblies;
                this.compositionVersions = factAttribute.CompositionVersions;
                this.noCompatGoal = factAttribute.NoCompatGoal;
                this.invalidConfiguration = factAttribute.InvalidConfiguration;

                if (this.Traits.ContainsKey(Tests.Traits.SkipOnMono) && TestUtilities.IsOnMono)
                {
                    this.SkipReason = this.SkipReason ?? "Test marked as skipped on Mono runtime due to unsupported feature: " + string.Join(", ", this.Traits[Tests.Traits.SkipOnMono]);
                }

                if (this.Traits.ContainsKey(Tests.Traits.SkipOnCoreCLR) && TestUtilities.IsOnCoreCLR)
                {
                    this.SkipReason = this.SkipReason ?? "Test marked as skipped on CoreCLR runtime due to unsupported feature: " + string.Join(", ", this.Traits[Tests.Traits.SkipOnCoreCLR]);
                }
            }

            public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                var runSummary = new RunSummary();

                if (this.SkipReason != null)
                {
                    runSummary.Skipped++;
                    runSummary.Total++;
                    if (!messageBus.QueueMessage(new TestSkipped(new XunitTest(this, this.DisplayName), this.SkipReason)))
                    {
                        cancellationTokenSource.Cancel();
                    }

                    return runSummary;
                }

                if (this.parts == null && this.assemblies == null)
                {
                    this.parts = GetNestedTypesRecursively(this.TestMethod.TestClass.Class.ToRuntimeType()).Where(t => (!t.GetTypeInfo().IsAbstract || t.GetTypeInfo().IsSealed) && !t.GetTypeInfo().IsInterface).ToArray();
                }

#if DESKTOP
                if (this.compositionVersions.HasFlag(CompositionEngines.V1))
                {
                    var runner = new LegacyMefTestCaseRunner(this, "V1", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, CompositionEngines.V1, this.parts, this.assemblies, this.invalidConfiguration);
                    runSummary.Aggregate(await runner.RunAsync());
                }
#endif

                if (this.compositionVersions.HasFlag(CompositionEngines.V2))
                {
                    var runner = new LegacyMefTestCaseRunner(this, "V2", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, CompositionEngines.V2, this.parts, this.assemblies, this.invalidConfiguration);
                    runSummary.Aggregate(await runner.RunAsync());
                }

                if ((this.compositionVersions & CompositionEngines.V3EnginesMask) == CompositionEngines.Unspecified)
                {
                    if (!this.noCompatGoal)
                    {
                        // Call out that we're *not* testing V3 functionality for this test.
                        if (!messageBus.QueueMessage(new TestSkipped(new XunitTest(this, "V3"), "Test does not include V3 test.")))
                        {
                            cancellationTokenSource.Cancel();
                        }
                    }
                }
                else
                {
                    var v3DiscoveryTest = new Mef3DiscoveryTestCaseRunner(this, "V3 composition", null, constructorArguments, messageBus, aggregator, cancellationTokenSource, this.compositionVersions, this.parts ?? new Type[0], this.assemblies ?? ImmutableList<string>.Empty, this.invalidConfiguration);
                    runSummary.Aggregate(await v3DiscoveryTest.RunAsync());

                    if (v3DiscoveryTest.Passed && (!this.invalidConfiguration || this.compositionVersions.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors)))
                    {
                        foreach (var namedConfiguration in v3DiscoveryTest.ResultingConfigurations)
                        {
                            string name = "V3 engine";
                            if (!string.IsNullOrEmpty(namedConfiguration.Description))
                            {
                                name += $" ({namedConfiguration.Description})";
                            }

                            var runner = new Mef3TestCaseRunner(this, name, null, constructorArguments, messageBus, aggregator, cancellationTokenSource, namedConfiguration.Configuration, this.compositionVersions);
                            runSummary.Aggregate(await runner.RunAsync());
                        }
                    }
                }

                return runSummary;
            }

            public override void Serialize(IXunitSerializationInfo data)
            {
                base.Serialize(data);
                data.AddValue(nameof(this.parts), this.parts);
                data.AddValue(nameof(this.assemblies), this.assemblies?.ToArray());
                data.AddValue(nameof(this.compositionVersions), this.compositionVersions);
                data.AddValue(nameof(this.noCompatGoal), this.noCompatGoal);
                data.AddValue(nameof(this.invalidConfiguration), this.invalidConfiguration);
                data.AddValue(nameof(this.SkipReason), this.SkipReason);
            }

            public override void Deserialize(IXunitSerializationInfo data)
            {
                base.Deserialize(data);
                this.parts = data.GetValue<Type[]>(nameof(this.parts));
                this.assemblies = data.GetValue<string[]>(nameof(this.assemblies));
                this.compositionVersions = data.GetValue<CompositionEngines>(nameof(this.compositionVersions));
                this.noCompatGoal = data.GetValue<bool>(nameof(this.noCompatGoal));
                this.invalidConfiguration = data.GetValue<bool>(nameof(this.invalidConfiguration));
                this.SkipReason = data.GetValue<string>(nameof(this.SkipReason));
            }
        }
    }
}
