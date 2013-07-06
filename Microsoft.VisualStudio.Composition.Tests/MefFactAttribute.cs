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

            // TODO: when no V3 engine is selected, also produce a Skip command highlighting the fact.
        }
    }
}
