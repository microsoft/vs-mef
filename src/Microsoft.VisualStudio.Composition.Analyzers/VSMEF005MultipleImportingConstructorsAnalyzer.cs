// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    internal static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
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
            bool mefV1AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase));
            bool mefV2AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.Composition.AttributedModel", StringComparison.OrdinalIgnoreCase));
            if (mefV1AttributesPresent || mefV2AttributesPresent)
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context), SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;

        // Only analyze classes
        if (symbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        // Find all constructors with ImportingConstructor attributes
        var importingConstructors = new List<IMethodSymbol>();

        foreach (var constructor in symbol.Constructors)
        {
            if (constructor.IsStatic)
            {
                continue;
            }

            foreach (var attribute in constructor.GetAttributes())
            {
                if (IsImportingConstructorAttribute(attribute.AttributeClass))
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
            foreach (var constructor in importingConstructors)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptor,
                    constructor.Locations[0],
                    symbol.Name));
            }
        }
    }

    /// <summary>
    /// Determines whether the specified attribute type is an importing constructor attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is an importing constructor attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for both MEF v1 and MEF v2 importing constructor attributes, including custom attributes that derive from the base importing constructor attribute types.
    /// </remarks>
    private static bool IsImportingConstructorAttribute(INamedTypeSymbol? attributeType)
    {
        return IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV1ImportingConstructorNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV2ImportingConstructorNamespace.AsSpan());
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
