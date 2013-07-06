namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    public class CompatFactAttribute : FactAttribute
    {
        private CompositionEngines compositionVersions;

        public CompatFactAttribute(CompositionEngines compositionVersions)
        {
            this.compositionVersions = compositionVersions;
        }

        protected override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo method)
        {
            foreach (var engine in new[] { CompositionEngines.V1, CompositionEngines.V2, CompositionEngines.V3EmulatingV1, CompositionEngines.V3EmulatingV2 })
            {
                if (this.compositionVersions.HasFlag(engine))
                {
                    yield return new CompatCommand(method, engine);
                }
            }
        }
    }
}
