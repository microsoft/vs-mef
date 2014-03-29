namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using Xunit;
    using Xunit.Sdk;

    public class MefTestCommand : FactCommand
    {
        private readonly CompositionEngines engineVersion;
        private readonly Type[] parts;
        private readonly ImmutableArray<string> assemblies;
        private readonly bool invalidConfiguration;

        public MefTestCommand(IMethodInfo method, CompositionEngines engineVersion, Type[] parts, ImmutableArray<string> assemblies, bool invalidConfiguration)
            : base(method)
        {
            Requires.Argument(parts != null || !assemblies.IsDefault, "parts ?? assemblies", "Either parameter must be non-null.");

            this.engineVersion = engineVersion;
            this.parts = parts;
            this.assemblies = assemblies;
            this.DisplayName = method.Class.Type.Name + "." + method.Name + " " + engineVersion;
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
                        RunMultiEngineTest(
                            this.engineVersion,
                            this.parts,
                            this.assemblies,
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
                RunMultiEngineTest(
                    this.engineVersion,
                    this.parts,
                    this.assemblies,
                    container => this.testMethod.Invoke(testClass, container));
            }

            return new PassedResult(this.testMethod, this.DisplayName);
        }

        private static void RunMultiEngineTest(CompositionEngines attributesVersion, Type[] parts, ImmutableArray<string> assemblies, Action<IContainer> test)
        {
            parts = parts ?? new Type[0];
            var loadedAssemblies = assemblies.Select(Assembly.Load).ToImmutableArray();
            TestUtilities.RunMultiEngineTest(attributesVersion, loadedAssemblies, parts, test);
        }
    }
}
