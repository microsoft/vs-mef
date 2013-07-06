namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit.Sdk;

    public class CompatCommand : FactCommand
    {
        private CompositionEngines engineVersion;

        public CompatCommand(IMethodInfo method, CompositionEngines engineVersion)
            : base(method)
        {
            this.engineVersion = engineVersion;
            this.DisplayName += " " + engineVersion;
        }

        public override MethodResult Execute(object testClass)
        {
            TestUtilities.RunMultiEngineTest(
                this.engineVersion,
                this.testMethod.Class.Type.GetNestedTypes(),
                container => this.testMethod.Invoke(testClass, container));

            return new PassedResult(this.testMethod, this.DisplayName);
        }
    }
}
