namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using Xunit;
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MefFactAttribute : FactAttribute
    {
        private readonly CompositionEngines compositionVersions;
        private readonly Type[] parts;
        private readonly IReadOnlyList<string> assemblies;

        public MefFactAttribute(CompositionEngines compositionVersions)
        {
            this.compositionVersions = compositionVersions;
        }

        public MefFactAttribute(CompositionEngines compositionVersions, params Type[] parts)
            : this(compositionVersions)
        {
            Requires.NotNull(parts, "parts");

            this.parts = parts;
            this.assemblies = ImmutableList<string>.Empty;
        }

        public MefFactAttribute(CompositionEngines compositionVersions, string newLineSeparatedAssemblyNames, params Type[] parts)
            : this(compositionVersions, parts)
        {
            Requires.NotNullOrEmpty(newLineSeparatedAssemblyNames, "newLineSeparatedAssemblyNames");
            this.assemblies = newLineSeparatedAssemblyNames
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToImmutableList();
        }

        public MefFactAttribute(CompositionEngines compositionVersions, params string[] assemblies)
            : this(compositionVersions)
        {
            Requires.NotNull(assemblies, "assemblies");

            this.assemblies = assemblies.ToImmutableList();
        }

        public bool InvalidConfiguration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to suppress claiming the test is skipped before it includes V3 runs.
        /// </summary>
        public bool NoCompatGoal { get; set; }

        protected override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo method)
        {
            var parts = this.parts;
            if (parts == null && this.assemblies == null)
            {
                parts = GetNestedTypesRecursively(method.Class.Type).Where(t => (!t.IsAbstract || t.IsSealed) && !t.IsInterface).ToArray();
            }

            if (this.compositionVersions.HasFlag(CompositionEngines.V1))
            {
                yield return new MefTestCommand(method, CompositionEngines.V1, parts, this.assemblies, this.InvalidConfiguration);
            }

            if (this.compositionVersions.HasFlag(CompositionEngines.V2))
            {
                yield return new MefTestCommand(method, CompositionEngines.V2, parts, this.assemblies, this.InvalidConfiguration);
            }

            if ((this.compositionVersions & CompositionEngines.V3EnginesMask) == CompositionEngines.Unspecified)
            {
                if (!this.NoCompatGoal)
                {
                    // Call out that we're *not* testing V3 functionality for this test.
                    yield return new SkipCommand(method, MethodUtility.GetDisplayName(method) + "V3", "Test does not include V3 test.");
                }
            }
            else
            {
                var v3DiscoveryTest = new MefV3DiscoveryTestCommand(method, this.compositionVersions, parts ?? new Type[0], this.assemblies ?? ImmutableList<string>.Empty, this.InvalidConfiguration);
                yield return v3DiscoveryTest;

                if (v3DiscoveryTest.Result is PassedResult && (!this.InvalidConfiguration || this.compositionVersions.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors)))
                {
                    foreach (var configuration in v3DiscoveryTest.ResultingConfigurations)
                    {
                        if (!this.compositionVersions.HasFlag(CompositionEngines.V3SkipCodeGenScenario))
                        {
                            // TODO: Uncomment this line after getting codegen to work again.
                            //       Also re-enable some codegen tests by removing 'abstract' from classes that have this comment:
                            //       // TODO: remove "abstract" from the class definition to re-enable these tests when codegen is fixed.
                            ////yield return new Mef3TestCommand(method, configuration, this.compositionVersions, runtime: false);
                        }

                        yield return new Mef3TestCommand(method, configuration, this.compositionVersions, runtime: true);
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
