namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using DiffPlex;
    using DiffPlex.DiffBuilder;
    using DiffPlex.DiffBuilder.Model;
    using Validation;
    using Xunit;
    using Xunit.Sdk;

    public class MefV3DiscoveryTestCommand : FactCommand
    {
        private readonly CompositionEngines compositionVersions;
        private readonly bool expectInvalidConfiguration;
        private readonly Type[] parts;
        private readonly IReadOnlyList<string> assemblyNames;

        public MefV3DiscoveryTestCommand(IMethodInfo method, CompositionEngines compositionEngines, Type[] parts, IReadOnlyList<string> assemblyNames, bool expectInvalidConfiguration)
            : base(method)
        {
            Requires.NotNull(method, "method");
            Requires.NotNull(parts, "parts");
            Requires.NotNull(assemblyNames, "assemblyNames");

            this.compositionVersions = compositionEngines;
            this.assemblyNames = assemblyNames;
            this.parts = parts;
            this.expectInvalidConfiguration = expectInvalidConfiguration;

            this.DisplayName = "V3 composition";
        }

        public MethodResult Result { get; set; }

        public IReadOnlyList<CompositionConfiguration> ResultingConfigurations { get; set; }

        public override MethodResult Execute(object testClass)
        {
            try
            {
                var v3DiscoveryModules = this.GetV3DiscoveryModules();

                var resultingCatalogs = new List<ComposableCatalog>(v3DiscoveryModules.Count);

                var assemblies = this.assemblyNames.Select(Assembly.Load).ToList();
                foreach (var discoveryModule in v3DiscoveryModules)
                {
                    var partsFromTypes = discoveryModule.CreatePartsAsync(this.parts).GetAwaiter().GetResult();
                    var partsFromAssemblies = discoveryModule.CreatePartsAsync(assemblies).GetAwaiter().GetResult();
                    var catalog = ComposableCatalog.Create()
                        .WithParts(partsFromTypes)
                        .WithParts(partsFromAssemblies);
                    resultingCatalogs.Add(catalog);
                }

                string[] catalogStringRepresentations = resultingCatalogs.Select(catalog =>
                    {
                        var writer = new StringWriter();
                        catalog.ToString(writer);
                        return writer.ToString();
                    }).ToArray();

                bool anyStringRepresentationDifferences = false;
                for (int i = 1; i < resultingCatalogs.Count; i++)
                {
                    var d = new Differ();
                    var inlineBuilder = new InlineDiffBuilder(d);
                    var result = inlineBuilder.BuildDiffModel(catalogStringRepresentations[0], catalogStringRepresentations[i]);
                    if (result.Lines.Any(l => l.Type != ChangeType.Unchanged))
                    {
                        Console.WriteLine("Catalog {0} vs. {1}", v3DiscoveryModules[0], v3DiscoveryModules[i]);
                        foreach (var line in result.Lines)
                        {
                            if (line.Type == ChangeType.Inserted)
                            {
                                Console.Write("+ ");
                            }
                            else if (line.Type == ChangeType.Deleted)
                            {
                                Console.Write("- ");
                            }
                            else
                            {
                                Console.Write("  ");
                            }

                            Console.WriteLine(line.Text);
                        }

                        anyStringRepresentationDifferences = true;
                        ////Assert.False(anyStringRepresentationDifferences, "Catalogs not equivalent");
                    }
                }

                // Verify that the catalogs are identical.
                // The string compare above should have taken care of this (in a more descriptive way),
                // but we do this to double-check.
                var uniqueCatalogs = resultingCatalogs.Distinct().ToArray();

                // Fail the test if ComposableCatalog.Equals returns a different result from string comparison.
                Assert.Equal(anyStringRepresentationDifferences, uniqueCatalogs.Length > 1);

                if (uniqueCatalogs.Length == 1)
                {
                    ////Console.WriteLine(catalogStringRepresentations[0]);
                }

                // For each distinct catalog, create one configuration and verify it meets expectations.
                var configurations = new List<CompositionConfiguration>(uniqueCatalogs.Length);
                foreach (var uniqueCatalog in uniqueCatalogs)
                {
                    var catalogWithSupport = uniqueCatalog
                        .WithCompositionService()
                        .WithDesktopSupport();
                    var configuration = CompositionConfiguration.Create(catalogWithSupport);

                    if (!this.compositionVersions.HasFlag(CompositionEngines.V3AllowConfigurationWithErrors))
                    {
                        Assert.Equal(this.expectInvalidConfiguration, !configuration.CompositionErrors.IsEmpty || !catalogWithSupport.DiscoveredParts.DiscoveryErrors.IsEmpty);
                    }

                    // Save the configuration in a property so that the engine test that follows can reuse the work we've done.
                    configurations.Add(configuration);
                }

                this.ResultingConfigurations = configurations;
                return this.Result = new PassedResult(this.testMethod, this.DisplayName);
            }
            catch (Exception ex)
            {
                return this.Result = new FailedResult(this.testMethod, ex, this.DisplayName);
            }
        }

        private IReadOnlyList<PartDiscovery> GetV3DiscoveryModules()
        {
            var titleAppends = new List<string>();

            var discovery = new List<PartDiscovery>();
            if (this.compositionVersions.HasFlag(CompositionEngines.V3EmulatingV1))
            {
                discovery.Add(new AttributedPartDiscoveryV1());
                titleAppends.Add("V1");
            }

            if (this.compositionVersions.HasFlag(CompositionEngines.V3EmulatingV2))
            {
                discovery.Add(new AttributedPartDiscovery { IsNonPublicSupported = compositionVersions.HasFlag(CompositionEngines.V3EmulatingV2WithNonPublic) });
                titleAppends.Add("V2");
            }

            if (this.compositionVersions.HasFlag(CompositionEngines.V3EmulatingV1AndV2AtOnce))
            {
                discovery.Add(PartDiscovery.Combine(
                    new AttributedPartDiscoveryV1(),
                    new AttributedPartDiscovery { IsNonPublicSupported = compositionVersions.HasFlag(CompositionEngines.V3EmulatingV2WithNonPublic) }));
                titleAppends.Add("V1+V2");
            }

            this.DisplayName += " (" + string.Join(", ", titleAppends) + ")";

            return discovery;
        }
    }
}
