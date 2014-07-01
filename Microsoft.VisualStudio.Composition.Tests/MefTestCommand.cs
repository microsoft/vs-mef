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
        private readonly IReadOnlyList<string> assemblies;
        private readonly bool invalidConfiguration;

        public MefTestCommand(IMethodInfo method, CompositionEngines engineVersion, Type[] parts, IReadOnlyList<string> assemblies, bool invalidConfiguration)
            : base(method)
        {
            Requires.Argument(parts != null || assemblies != null, "parts ?? assemblies", "Either parameter must be non-null.");

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
                        CompositionConfiguration.Create(ComposableCatalog.Create(new AttributedPartDiscoveryV1(), this.parts))
                            .ThrowOnErrors();
                    }
                    else if (this.engineVersion == CompositionEngines.V3EmulatingV2)
                    {
                        CompositionConfiguration.Create(ComposableCatalog.Create(new AttributedPartDiscovery(), this.parts))
                            .ThrowOnErrors();
                    }
                    else if (this.engineVersion == CompositionEngines.V3EmulatingV1AndV2AtOnce)
                    {
                        CompositionConfiguration.Create(ComposableCatalog.Create(PartDiscovery.Combine(new AttributedPartDiscoveryV1(), new AttributedPartDiscovery()), this.parts))
                            .ThrowOnErrors();
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

        private static void RunMultiEngineTest(CompositionEngines attributesVersion, Type[] parts, IReadOnlyList<string> assemblies, Action<IContainer> test)
        {
            parts = parts ?? new Type[0];
            var loadedAssemblies = assemblies != null ? assemblies.Select(Assembly.Load).ToImmutableList() : ImmutableList<Assembly>.Empty;
            TestUtilities.RunMultiEngineTest(attributesVersion, loadedAssemblies, parts, test);
        }
    }
}
