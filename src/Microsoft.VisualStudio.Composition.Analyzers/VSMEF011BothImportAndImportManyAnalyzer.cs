// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when both Import and ImportMany attributes are applied to the same member.
/// </summary>
/// <remarks>
/// <para>
/// The [Import] and [ImportMany] attributes are mutually exclusive. A member cannot have both
/// because MEF cannot determine whether to import a single value or a collection.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF011BothImportAndImportManyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF011";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF011_Title,
        messageFormat: Strings.VSMEF011_MessageFormat,
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
            if (!Utils.ReferencesMefAttributes(context.Compilation))
            {
                return;
            }

            INamedTypeSymbol? mefV1ImportAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportAttribute");
            INamedTypeSymbol? mefV2ImportAttribute = context.Compilation.GetTypeByMetadataName("System.Composition.ImportAttribute");
            INamedTypeSymbol? mefV1ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportManyAttribute");
            INamedTypeSymbol? mefV2ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.Composition.ImportManyAttribute");

            if ((mefV1ImportAttribute is null && mefV2ImportAttribute is null) ||
                (mefV1ImportManyAttribute is null && mefV2ImportManyAttribute is null))
            {
                return;
            }

            var state = new AnalyzerState(
                mefV1ImportAttribute,
                mefV2ImportAttribute,
                mefV1ImportManyAttribute,
                mefV2ImportManyAttribute);

            context.RegisterSymbolAction(ctx => AnalyzeProperty(ctx, state), SymbolKind.Property);
            context.RegisterSymbolAction(ctx => AnalyzeField(ctx, state), SymbolKind.Field);
            context.RegisterSymbolAction(ctx => AnalyzeMethod(ctx, state), SymbolKind.Method);
        });
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, AnalyzerState state)
    {
        var property = (IPropertySymbol)context.Symbol;
        CheckForConflictingImportAttributes(context, property.Name, property.GetAttributes(), property.Locations.FirstOrDefault(), state);
    }

    private static void AnalyzeField(SymbolAnalysisContext context, AnalyzerState state)
    {
        var field = (IFieldSymbol)context.Symbol;
        CheckForConflictingImportAttributes(context, field.Name, field.GetAttributes(), field.Locations.FirstOrDefault(), state);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, AnalyzerState state)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Only analyze constructors with ImportingConstructor attribute
        if (method.MethodKind != MethodKind.Constructor || !Utils.HasImportingConstructorAttribute(method))
        {
            return;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            CheckForConflictingImportAttributes(context, parameter.Name, parameter.GetAttributes(), parameter.Locations.FirstOrDefault(), state);
        }
    }

    private static void CheckForConflictingImportAttributes(
        SymbolAnalysisContext context,
        string memberName,
        ImmutableArray<AttributeData> attributes,
        Location? location,
        AnalyzerState state)
    {
        if (location is null)
        {
            return;
        }

        bool hasImport = false;
        bool hasImportMany = false;

        foreach (AttributeData attr in attributes)
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV1ImportAttribute) ||
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV2ImportAttribute))
            {
                hasImport = true;
            }

            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV1ImportManyAttribute) ||
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV2ImportManyAttribute))
            {
                hasImportMany = true;
            }
        }

        if (hasImport && hasImportMany)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptor,
                location,
                memberName));
        }
    }

    private sealed record AnalyzerState(
        INamedTypeSymbol? MefV1ImportAttribute,
        INamedTypeSymbol? MefV2ImportAttribute,
        INamedTypeSymbol? MefV1ImportManyAttribute,
        INamedTypeSymbol? MefV2ImportManyAttribute);
}
