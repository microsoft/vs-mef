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
        private readonly ImmutableArray<string> assemblies;

        public MefFactAttribute(CompositionEngines compositionVersions)
        {
            this.compositionVersions = compositionVersions;
        }

        public MefFactAttribute(CompositionEngines compositionVersions, params Type[] parts)
            : this(compositionVersions)
        {
            Requires.NotNull(parts, "parts");

            this.parts = parts;
        }

        public MefFactAttribute(CompositionEngines compositionVersions, string newLineSeparatedAssemblyNames, params Type[] parts)
            : this(compositionVersions, parts)
        {
            Requires.NotNullOrEmpty(newLineSeparatedAssemblyNames, "newLineSeparatedAssemblyNames");
            this.assemblies = newLineSeparatedAssemblyNames
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToImmutableArray();
        }

        public MefFactAttribute(CompositionEngines compositionVersions, params string[] assemblies)
            : this(compositionVersions)
        {
            Requires.NotNull(assemblies, "assemblies");

            this.assemblies = assemblies.ToImmutableArray();
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
                parts = method.Class.Type.GetNestedTypes().Where(t => (!t.IsAbstract || t.IsSealed) && !t.IsInterface).ToArray();
            }

            foreach (var engine in new[] { CompositionEngines.V1, CompositionEngines.V2, CompositionEngines.V3EmulatingV1, CompositionEngines.V3EmulatingV2 })
            {
                if (this.compositionVersions.HasFlag(engine))
                {
                    yield return new MefTestCommand(method, engine, parts, this.assemblies, this.InvalidConfiguration);
                }
            }

            if (!this.NoCompatGoal)
            {
                // Call out that we're *not* testing V3 functionality for this test.
                if ((this.compositionVersions & (CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1)) == CompositionEngines.Unspecified)
                {
                    yield return new SkipCommand(method, MethodUtility.GetDisplayName(method) + "V3", "Test does not include V3 test.");
                }
            }
        }
    }
}
