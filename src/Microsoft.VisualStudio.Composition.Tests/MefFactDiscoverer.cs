namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class MefFactDiscoverer : IXunitTestCaseDiscoverer
    {
        readonly IMessageSink diagnosticMessageSink;

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
            var factAttribute = MefFactAttribute.Instantiate(factAttributeInfo);

            var parts = factAttribute.Parts;
            if (parts == null && factAttribute.Assemblies == null)
            {
                parts = GetNestedTypesRecursively(method.Class.Type).Where(t => (!t.IsAbstract || t.IsSealed) && !t.IsInterface).ToArray();
            }

            if (factAttribute.CompositionVersions.HasFlag(CompositionEngines.V1))
            {
                yield return new MefTestCommand(method, CompositionEngines.V1, parts, factAttribute.Assemblies, factAttribute.InvalidConfiguration);
            }

            if (factAttribute.CompositionVersions.HasFlag(CompositionEngines.V2))
            {
                yield return new MefTestCommand(method, CompositionEngines.V2, parts, factAttribute.Assemblies, factAttribute.InvalidConfiguration);
            }

            if ((factAttribute.CompositionVersions & CompositionEngines.V3EnginesMask) == CompositionEngines.Unspecified)
            {
                if (!factAttribute.NoCompatGoal)
                {
                    // Call out that we're *not* testing V3 functionality for this test.
                    yield return new SkipCommand(method, MethodUtility.GetDisplayName(method) + "V3", "Test does not include V3 test.");
                }
            }
            else
            {
                var v3DiscoveryTest = new MefV3DiscoveryTestCommand(method, factAttribute.CompositionVersions, parts ?? new Type[0], factAttribute.Assemblies ?? ImmutableList<string>.Empty, factAttribute.InvalidConfiguration);
                yield return v3DiscoveryTest;

                if (v3DiscoveryTest.Result is PassedResult && (!factAttribute.InvalidConfiguration || factAttribute.compositionVersions.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors)))
                {
                    foreach (var configuration in v3DiscoveryTest.ResultingConfigurations)
                    {
                        if (!factAttribute.compositionVersions.HasFlag(CompositionEngines.V3SkipCodeGenScenario))
                        {
                            // TODO: Uncomment this line after getting codegen to work again.
                            //       Also re-enable some codegen tests by removing 'abstract' from classes that have this comment:
                            //       // TODO: remove "abstract" from the class definition to re-enable these tests when codegen is fixed.
                            ////yield return new Mef3TestCommand(method, configuration, this.compositionVersions, runtime: false);
                        }

                        yield return new Mef3TestCommand(method, configuration, factAttribute.CompositionVersions, runtime: true);
                    }
                }
            }
        }

        private static IEnumerable<Type> GetNestedTypesRecursively(Type parentType)
        {
            Requires.NotNull(parentType, "parentType");

            foreach (var nested in parentType.GetNestedTypes())
            {
                yield return nested;

                foreach (var recursive in GetNestedTypesRecursively(nested))
                {
                    yield return recursive;
                }
            }
        }
    }
}
