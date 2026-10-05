// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.CSharp;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Reports diagnostics for metadata view interfaces that should participate in source generation but cannot.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class VSMEF015MetadataViewSourceGeneratorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic ID for same-compilation metadata view interfaces that should be declared <c>partial</c> and annotated with <c>[MetadataView]</c>.
    /// </summary>
    public const string SameCompilationId = "VSMEF015";

    /// <summary>
    /// The diagnostic ID for metadata view interfaces declared in a referenced assembly that should be rebuilt with <c>[MetadataView]</c>.
    /// </summary>
    public const string ReferencedAssemblyId = "VSMEF016";

    /// <summary>
    /// The diagnostic ID for invalid <c>[MetadataView]</c> usage.
    /// </summary>
    public const string InvalidMetadataViewId = "VSMEF017";

    /// <summary>
    /// The descriptor for <see cref="SameCompilationId"/>.
    /// </summary>
    public static readonly DiagnosticDescriptor SameCompilationDescriptor = new(
        id: SameCompilationId,
        title: Strings.VSMEF015_Title,
        messageFormat: Strings.VSMEF015_MessageFormat,
        helpLinkUri: GetHelpLink(SameCompilationId),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// The descriptor for <see cref="ReferencedAssemblyId"/>.
    /// </summary>
    public static readonly DiagnosticDescriptor ReferencedAssemblyDescriptor = new(
        id: ReferencedAssemblyId,
        title: Strings.VSMEF016_Title,
        messageFormat: Strings.VSMEF016_MessageFormat,
        helpLinkUri: GetHelpLink(ReferencedAssemblyId),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// The descriptor for <see cref="InvalidMetadataViewId"/>.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMetadataViewDescriptor = new(
        id: InvalidMetadataViewId,
        title: Strings.VSMEF017_Title,
        messageFormat: Strings.VSMEF017_MessageFormat,
        helpLinkUri: GetHelpLink(InvalidMetadataViewId),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        SameCompilationDescriptor,
        ReferencedAssemblyDescriptor,
        InvalidMetadataViewDescriptor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (compilationContext.Compilation.GetTypeByMetadataName(Types.MetadataViewAttribute.FullName) is null
                || (compilationContext.Compilation.GetTypeByMetadataName(Types.ImportAttribute.FullName) is null
                    && compilationContext.Compilation.GetTypeByMetadataName(Types.ImportAttributeV2.FullName) is null))
            {
                return;
            }

            compilationContext.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
            compilationContext.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            compilationContext.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            compilationContext.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Interface } metadataViewInterface
            || !MetadataViewGeneratorUtilities.HasAttribute(metadataViewInterface, Types.MetadataViewAttribute.FullName))
        {
            return;
        }

        if (MetadataViewGeneratorUtilities.IsPartialInterfaceAndContainingTypes(metadataViewInterface, context.CancellationToken)
            && MetadataViewGeneratorUtilities.IsGeneratableMetadataViewInterface(metadataViewInterface))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            InvalidMetadataViewDescriptor,
            MetadataViewGeneratorUtilities.GetFirstInvalidMetadataViewLocation(metadataViewInterface, context.CancellationToken)));
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var field = (IFieldSymbol)context.Symbol;
        AnalyzeImport(context, field, field.Type, field.GetAttributes());
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        AnalyzeImport(context, property, property.Type, property.GetAttributes());
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Constructor && !MetadataViewGeneratorUtilities.HasImportingConstructorAttribute(method))
        {
            return;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            AnalyzeImport(context, parameter, parameter.Type, parameter.GetAttributes());
        }
    }

    private static void AnalyzeImport(SymbolAnalysisContext context, ISymbol targetSymbol, ITypeSymbol memberType, ImmutableArray<AttributeData> attributes)
    {
        AttributeData? importAttribute = MetadataViewGeneratorUtilities.GetImportAttribute(attributes);
        if (importAttribute is null)
        {
            return;
        }

        if (!MetadataViewGeneratorUtilities.TryGetMetadataViewInterface(
            memberType,
            MetadataViewGeneratorUtilities.IsImportManyAttribute(importAttribute.AttributeClass),
            out INamedTypeSymbol? metadataViewInterface))
        {
            return;
        }

        if (MetadataViewGeneratorUtilities.HasAttribute(metadataViewInterface, Types.MetadataViewImplementationAttribute.FullName))
        {
            return;
        }

        bool sameCompilation = SymbolEqualityComparer.Default.Equals(metadataViewInterface.ContainingAssembly, context.Compilation.Assembly);
        if (sameCompilation)
        {
            bool hasMetadataViewAttribute = MetadataViewGeneratorUtilities.HasAttribute(metadataViewInterface, Types.MetadataViewAttribute.FullName);
            if (hasMetadataViewAttribute)
            {
                if (MetadataViewGeneratorUtilities.IsPartialInterfaceAndContainingTypes(metadataViewInterface, context.CancellationToken)
                    && MetadataViewGeneratorUtilities.IsGeneratableMetadataViewInterface(metadataViewInterface))
                {
                    return;
                }

                // Let the declaration-site error describe why [MetadataView] is invalid.
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                SameCompilationDescriptor,
                targetSymbol.Locations[0],
                metadataViewInterface.ToDisplayString()));
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ReferencedAssemblyDescriptor,
            targetSymbol.Locations[0],
            metadataViewInterface.ToDisplayString(),
            metadataViewInterface.ContainingAssembly.Name));
    }

    private static string GetHelpLink(string analyzerId) => $"https://microsoft.github.io/vs-mef/analyzers/{analyzerId}.html";
}
