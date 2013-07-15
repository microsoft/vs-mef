namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    public class MefTestCommand : FactCommand
    {
        private readonly CompositionEngines engineVersion;
        private readonly Type[] parts;
        private readonly bool invalidConfiguration;

        public MefTestCommand(IMethodInfo method, CompositionEngines engineVersion, Type[] parts, bool invalidConfiguration)
            : base(method)
        {
            this.engineVersion = engineVersion;
            this.parts = parts;
            this.DisplayName += " " + engineVersion;
            this.invalidConfiguration = invalidConfiguration;
        }

        public override MethodResult Execute(object testClass)
        {
            if (this.invalidConfiguration)
            {
                bool exceptionThrown;
                try
                {
                    if (this.engineVersion == CompositionEngines.V3EmulatingV1)
                    {
                        CompositionConfiguration.Create(ComposableCatalog.Create(this.parts, new AttributedPartDiscoveryV1()));
                    }
                    else if (this.engineVersion == CompositionEngines.V3EmulatingV2)
                    {
                        CompositionConfiguration.Create(ComposableCatalog.Create(this.parts, new AttributedPartDiscovery()));
                    }
                    else
                    {
                        TestUtilities.RunMultiEngineTest(
                            this.engineVersion,
                            this.parts,
                            container => this.testMethod.Invoke(testClass, container));
                    }

                    exceptionThrown = false;
                }
                catch
                {
                    exceptionThrown = true;
                }

                Assert.True(exceptionThrown, "Composition exception expected but not thrown.");
            }
            else
            {
                TestUtilities.RunMultiEngineTest(
                    this.engineVersion,
                    this.parts,
                    container => this.testMethod.Invoke(testClass, container));
            }

            return new PassedResult(this.testMethod, this.DisplayName);
        }
    }
}
