// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when a type has multiple constructors annotated with <c>[ImportingConstructor]</c>.
/// </summary>
/// <remarks>
/// This analyzer detects when a class has more than one constructor marked with the <c>[ImportingConstructor]</c> attribute.
/// MEF requires that only one constructor per type be marked as the importing constructor.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF005MultipleImportingConstructorsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF005";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF005_Title,
        messageFormat: Strings.VSMEF005_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<string> MefV1ImportingConstructorNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> MefV2ImportingConstructorNamespace = ImmutableArray.Create("System", "Composition");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            // Only run if MEF assemblies are referenced
            if (Utils.ReferencesMefAttributes(context.Compilation))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context), SymbolKind.NamedType);
            }
        });

        static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;

            // Only analyze classes and structs (both can have constructors with ImportingConstructor)
            if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            {
                return;
            }

            // Find all constructors with ImportingConstructor attributes
            var importingConstructors = new List<IMethodSymbol>();

            foreach (IMethodSymbol constructor in symbol.Constructors)
            {
                if (constructor.IsStatic)
                {
                    continue;
                }

                foreach (AttributeData attribute in constructor.GetAttributes())
                {
                    if (Utils.IsImportingConstructorAttribute(attribute.AttributeClass))
                    {
                        importingConstructors.Add(constructor);
                        break; // Found one ImportingConstructor attribute on this constructor, no need to check for more
                    }
                }
            }

            // Report diagnostic if more than one constructor has ImportingConstructor attribute
            if (importingConstructors.Count > 1)
            {
                // Report on each importing constructor
                foreach (IMethodSymbol constructor in importingConstructors)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Descriptor,
                        constructor.Locations[0],
                        symbol.Name));
                }
            }
        }
    }
}
