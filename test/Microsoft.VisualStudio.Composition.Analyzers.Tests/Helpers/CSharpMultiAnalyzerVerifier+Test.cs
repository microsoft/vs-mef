// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Composition.Analyzers;

public static partial class CSharpMultiAnalyzerVerifier
{
    /// <summary>
    /// Test class that configures all VSMEF analyzers for simultaneous testing.
    /// </summary>
    public class Test : CSharpCodeFixTest<VSMEF001PropertyMustHaveSetter, EmptyCodeFixProvider, DefaultVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Test"/> class.
        /// </summary>
        public Test()
        {
            this.ReferenceAssemblies = ReferencesHelper.DefaultReferences;
            this.TestBehaviors |= TestBehaviors.SkipGeneratedCodeCheck;

            this.SolutionTransforms.Add((solution, projectId) =>
            {
                var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
                solution = solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp12));
                return solution;
            });

            this.TestState.AdditionalFilesFactories.Add(() =>
            {
                const string additionalFilePrefix = "AdditionalFiles.";
                return from resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames()
                       where resourceName.StartsWith(additionalFilePrefix, StringComparison.Ordinal)
                       let content = ReadManifestResource(Assembly.GetExecutingAssembly(), resourceName)
                       select (filename: resourceName.Substring(additionalFilePrefix.Length), SourceText.From(content));
            });
        }

        /// <inheritdoc/>
        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            return
            [
                new VSMEF001PropertyMustHaveSetter(),
                new VSMEF002AvoidMixingAttributeVarietiesAnalyzer(),
                new VSMEF003ExportTypeMismatchAnalyzer(),
                new VSMEF004ExportWithoutImportingConstructorAnalyzer(),
                new VSMEF005MultipleImportingConstructorsAnalyzer(),
                new VSMEF006ImportNullabilityAnalyzer(),
                new VSMEF007DuplicateImportAnalyzer(),
                new VSMEF008ImportContractTypeMismatchAnalyzer(),
                new VSMEF009ImportManyMemberCollectionTypeAnalyzer(),
                new VSMEF010ImportManyParameterCollectionTypeAnalyzer(),
                new VSMEF011BothImportAndImportManyAnalyzer(),
                new VSMEF012DisallowMefAttributeVersionAnalyzer(),
            ];
        }

        private static string ReadManifestResource(Assembly assembly, string resourceName)
        {
            using (var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName) ?? throw new ArgumentException("No such resource stream", nameof(resourceName))))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
