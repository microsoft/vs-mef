// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when an import's null-annotation doesn't match its AllowDefault setting.
/// </summary>
/// <remarks>
/// This analyzer detects when:
/// 1. An import is nullable but doesn't have AllowDefault = true
/// 2. An import has AllowDefault = true but isn't nullable
/// Only operates in nullable contexts.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF006ImportNullabilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF006";

    /// <summary>
    /// The descriptor for nullable import without AllowDefault.
    /// </summary>
    public static readonly DiagnosticDescriptor NullableWithoutAllowDefaultDescriptor = new(
        id: Id,
        title: Strings.VSMEF006_Title,
        messageFormat: Strings.VSMEF006_NullableWithoutAllowDefault_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// The descriptor for AllowDefault without nullable.
    /// </summary>
    public static readonly DiagnosticDescriptor AllowDefaultWithoutNullableDescriptor = new(
        id: Id,
        title: Strings.VSMEF006_Title,
        messageFormat: Strings.VSMEF006_AllowDefaultWithoutNullable_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        NullableWithoutAllowDefaultDescriptor,
        AllowDefaultWithoutNullableDescriptor);

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
                context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
                context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
                context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            }
        });

        static void AnalyzeField(SymbolAnalysisContext context)
        {
            var field = (IFieldSymbol)context.Symbol;
            AnalyzeMember(context, field, field.Type, field.GetAttributes());
        }

        static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var property = (IPropertySymbol)context.Symbol;
            AnalyzeMember(context, property, property.Type, property.GetAttributes());
        }

        static void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;

            // Check method parameters for [ImportingConstructor] methods or regular constructors
            if (method.MethodKind == MethodKind.Constructor || Utils.HasImportingConstructorAttribute(method))
            {
                foreach (IParameterSymbol parameter in method.Parameters)
                {
                    AnalyzeMember(context, parameter, parameter.Type, parameter.GetAttributes());
                }
            }
        }

        static void AnalyzeMember(SymbolAnalysisContext context, ISymbol member, ITypeSymbol type, ImmutableArray<AttributeData> attributes)
        {
            AttributeData? importAttribute = Utils.GetImportAttribute(attributes);
            if (importAttribute is null)
            {
                return;
            }

            bool isNullableReferenceType = IsNullableReferenceType(type);
            bool hasAllowDefault = Utils.GetAllowDefaultValue(importAttribute);

            if (isNullableReferenceType && !hasAllowDefault)
            {
                // Nullable reference type but no AllowDefault = true
                context.ReportDiagnostic(Diagnostic.Create(
                    NullableWithoutAllowDefaultDescriptor,
                    member.Locations[0],
                    member.Name));
            }
            else if (!isNullableReferenceType && hasAllowDefault && type.IsReferenceType)
            {
                // AllowDefault = true but not nullable reference type
                // Note: We only warn for reference types, not value types, since value types have default values
                context.ReportDiagnostic(Diagnostic.Create(
                    AllowDefaultWithoutNullableDescriptor,
                    member.Locations[0],
                    member.Name));
            }

            static bool IsNullableReferenceType(ITypeSymbol type)
            {
                // Only consider reference types with nullable annotation, not nullable value types
                // Nullable value types (like int?) are distinct types from their non-nullable counterparts
                // and should not trigger nullability warnings since they're explicitly requesting Nullable<T>
                return type.CanBeReferencedByName &&
                       type.NullableAnnotation == NullableAnnotation.Annotated &&
                       type.IsReferenceType;
            }
        }
    }
}
