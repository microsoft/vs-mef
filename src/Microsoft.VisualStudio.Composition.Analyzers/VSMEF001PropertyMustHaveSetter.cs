// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers
{
    /// <summary>
    /// Creates a diagnostic when `[Import]` is applied to a property with no setter.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class VSMEF001PropertyMustHaveSetter : DiagnosticAnalyzer
    {
        /// <summary>
        /// The ID for diagnostics reported by this analyzer.
        /// </summary>
        public const string Id = "VSMEF001";

        /// <summary>
        /// The descriptor used for diagnostics created by this rule.
        /// </summary>
        internal static readonly DiagnosticDescriptor Descriptor = new(
            id: Id,
            title: Strings.VSMEF001_Title,
            messageFormat: Strings.VSMEF001_MessageFormat,
            helpLinkUri: Utils.GetHelpLink(Id),
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                // Only scan further if the compilation references the assemblies that define the attributes we'll be looking for.
                if (Utils.ReferencesMefAttributes(context.Compilation))
                {
                    INamedTypeSymbol? mefV1ImportAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportAttribute");
                    INamedTypeSymbol? mefV2ImportAttribute = context.Compilation.GetTypeByMetadataName("System.Composition.ImportAttribute");
                    context.RegisterSymbolAction(
                        context => AnalyzePropertyDeclaration(context, mefV1ImportAttribute, mefV2ImportAttribute),
                        SymbolKind.Property);
                }
            });
        }

        private static void AnalyzePropertyDeclaration(SymbolAnalysisContext context, INamedTypeSymbol? mefV1ImportAttribute, INamedTypeSymbol? mefV2ImportAttribute)
        {
            var property = (IPropertySymbol)context.Symbol;

            // If this property defines a setter, they aren't a candidate for a diagnostic.
            if (property.SetMethod is object)
            {
                return;
            }

            Location? location = property.Locations.FirstOrDefault();
            if (location is null)
            {
                // We won't have anywhere to publish a diagnostic anyway.
                return;
            }

            foreach (AttributeData attributeData in property.GetAttributes())
            {
                // Does this property have an Import attribute?
                if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, mefV1ImportAttribute) ||
                    SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, mefV2ImportAttribute))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptor, location));
                }
            }
        }
    }
}
