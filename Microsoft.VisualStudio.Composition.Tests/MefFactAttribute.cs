namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
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

        protected override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo method)
        {
            var parts = this.parts ?? method.Class.Type.GetNestedTypes().Where(t => !t.IsAbstract && !t.IsInterface).ToArray();
            foreach (var engine in new[] { CompositionEngines.V1, CompositionEngines.V2, CompositionEngines.V3EmulatingV1, CompositionEngines.V3EmulatingV2 })
            {
                if (this.compositionVersions.HasFlag(engine))
                {
                    yield return new MefTestCommand(method, engine, parts);
                }
            }

            // Call out that we're *not* testing V3 functionality for this test.
            if ((this.compositionVersions & (CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1)) == CompositionEngines.Unspecified)
            {
                yield return new SkipCommand(method, MethodUtility.GetDisplayName(method) + "V3", "Test does not include V3 test.");
            }
        }
    }
}
