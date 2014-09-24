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
            this.DisplayName = engineVersion.ToString();
            this.invalidConfiguration = invalidConfiguration;
        }

        public override MethodResult Execute(object testClass)
        {
            if (this.invalidConfiguration)
            {
                bool compositionExceptionThrown;
                try
                {
                    RunMultiEngineTest(
                        this.engineVersion,
                        this.parts,
                        this.assemblies,
                        container => this.testMethod.Invoke(testClass, container));

                    compositionExceptionThrown = false;
                }
                catch (CompositionFailedException)
                {
                    compositionExceptionThrown = true;
                }
                catch (InvalidOperationException)
                {
                    // MEFv1 throws this sometimes (CustomMetadataValueV1 test).
                    compositionExceptionThrown = true;
                }
                catch (System.Composition.Hosting.CompositionFailedException)
                {
                    // MEFv2 can throw this from ExportFactory`1.CreateExport.
                    compositionExceptionThrown = true;
                }

                Assert.True(compositionExceptionThrown, "Composition exception expected but not thrown.");
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
