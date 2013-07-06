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
            if (this.compositionVersions.HasFlag(CompositionEngines.V1))
            {
                yield return new CompatCommand(method, CompositionEngines.V1);
            }

            if (this.compositionVersions.HasFlag(CompositionEngines.V2))
            {
                yield return new CompatCommand(method, CompositionEngines.V2);
            }
        }
    }
}
