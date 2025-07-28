// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
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
    public static readonly DiagnosticDescriptor NullableWithoutAllowDefaultDescriptor = new DiagnosticDescriptor(
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
    public static readonly DiagnosticDescriptor AllowDefaultWithoutNullableDescriptor = new DiagnosticDescriptor(
        id: Id,
        title: Strings.VSMEF006_Title,
        messageFormat: Strings.VSMEF006_AllowDefaultWithoutNullable_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<string> MefV1ImportNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> MefV2ImportNamespace = ImmutableArray.Create("System", "Composition");

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
            bool mefV1AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase));
            bool mefV2AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.Composition.AttributedModel", StringComparison.OrdinalIgnoreCase));
            if (mefV1AttributesPresent || mefV2AttributesPresent)
            {
                context.RegisterSymbolAction(context => AnalyzeField(context), SymbolKind.Field);
                context.RegisterSymbolAction(context => AnalyzeProperty(context), SymbolKind.Property);
                context.RegisterSymbolAction(context => AnalyzeMethod(context), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var field = (IFieldSymbol)context.Symbol;
        AnalyzeMember(context, field, field.Type, field.GetAttributes());
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        AnalyzeMember(context, property, property.Type, property.GetAttributes());
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Check method parameters for [ImportingConstructor] methods or regular constructors
        if (method.MethodKind == MethodKind.Constructor || HasImportingConstructorAttribute(method))
        {
            foreach (var parameter in method.Parameters)
            {
                AnalyzeMember(context, parameter, parameter.Type, parameter.GetAttributes());
            }
        }
    }

    private static void AnalyzeMember(SymbolAnalysisContext context, ISymbol member, ITypeSymbol type, ImmutableArray<AttributeData> attributes)
    {
        var importAttribute = GetImportAttribute(attributes);
        if (importAttribute is null)
        {
            return;
        }

        bool isNullable = IsNullableType(type);
        bool hasAllowDefault = GetAllowDefaultValue(importAttribute);

        if (isNullable && !hasAllowDefault)
        {
            // Nullable type but no AllowDefault = true
            context.ReportDiagnostic(Diagnostic.Create(
                NullableWithoutAllowDefaultDescriptor,
                member.Locations[0],
                member.Name));
        }
        else if (!isNullable && hasAllowDefault)
        {
            // AllowDefault = true but not nullable
            context.ReportDiagnostic(Diagnostic.Create(
                AllowDefaultWithoutNullableDescriptor,
                member.Locations[0],
                member.Name));
        }

        static bool IsNullableType(ITypeSymbol type)
        {
            return type.CanBeReferencedByName && type.NullableAnnotation == NullableAnnotation.Annotated;
        }
    }

    private static AttributeData? GetImportAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.FirstOrDefault(attr => IsImportAttribute(attr.AttributeClass));
    }

    private static bool IsImportAttribute(INamedTypeSymbol? attributeType)
    {
        return IsAttributeOfType(attributeType, "ImportAttribute", MefV1ImportNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportAttribute", MefV2ImportNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportManyAttribute", MefV1ImportNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportManyAttribute", MefV2ImportNamespace.AsSpan());
    }

    private static bool GetAllowDefaultValue(AttributeData importAttribute)
    {
        var allowDefaultArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowDefault");
        if (allowDefaultArg.Key is not null && allowDefaultArg.Value.Value is bool allowDefault)
        {
            return allowDefault;
        }

        return false; // Default value is false
    }

    private static bool HasImportingConstructorAttribute(IMethodSymbol method)
    {
        return method.GetAttributes().Any(attr => IsImportingConstructorAttribute(attr.AttributeClass));
    }

    private static bool IsImportingConstructorAttribute(INamedTypeSymbol? attributeType)
    {
        return IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV1ImportNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV2ImportNamespace.AsSpan());
    }

    /// <summary>
    /// Determines whether the specified attribute type matches the expected type and namespace, including inheritance hierarchy.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <param name="attributeTypeName">The expected attribute type name.</param>
    /// <param name="expectedNamespace">The expected namespace components.</param>
    /// <returns><see langword="true"/> if the attribute type matches; otherwise, <see langword="false"/>.</returns>
    private static bool IsAttributeOfType(INamedTypeSymbol? attributeType, string attributeTypeName, ReadOnlySpan<string> expectedNamespace)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check the attribute type itself and its base types
        var current = attributeType;
        while (current is not null)
        {
            if (current.Name == attributeTypeName && IsNamespaceMatch(current.ContainingNamespace, expectedNamespace))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified namespace matches the expected namespace components.
    /// </summary>
    /// <param name="actual">The actual namespace to check.</param>
    /// <param name="expectedNames">The expected namespace components in reverse order (leaf to root).</param>
    /// <returns><see langword="true"/> if the namespace matches; otherwise, <see langword="false"/>.</returns>
    private static bool IsNamespaceMatch(INamespaceSymbol? actual, ReadOnlySpan<string> expectedNames)
    {
        if (actual is null or { IsGlobalNamespace: true })
        {
            return expectedNames.Length == 0;
        }

        if (expectedNames.Length == 0)
        {
            return false;
        }

        if (actual.Name != expectedNames[expectedNames.Length - 1])
        {
            return false;
        }

        return IsNamespaceMatch(actual.ContainingNamespace, expectedNames.Slice(0, expectedNames.Length - 1));
    }
}
