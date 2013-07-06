namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit.Sdk;

    public class MefTestCommand : FactCommand
    {
        private readonly CompositionEngines engineVersion;
        private readonly Type[] parts;

        public MefTestCommand(IMethodInfo method, CompositionEngines engineVersion, Type[] parts)
            : base(method)
        {
            this.engineVersion = engineVersion;
            this.parts = parts;
            this.DisplayName += " " + engineVersion;
        }

        public override MethodResult Execute(object testClass)
        {
            TestUtilities.RunMultiEngineTest(
                this.engineVersion,
                this.parts,
                container => this.testMethod.Invoke(testClass, container));

            return new PassedResult(this.testMethod, this.DisplayName);
        }
    }
}
