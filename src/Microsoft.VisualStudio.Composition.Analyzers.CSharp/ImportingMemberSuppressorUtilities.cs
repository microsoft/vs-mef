// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

internal static class ImportingMemberSuppressorUtilities
{
    private static readonly ImmutableArray<string> MefV1AttributeNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> MefV2AttributeNamespace = ImmutableArray.Create("System", "Composition");

    internal static bool HasInstanceExports(INamedTypeSymbol symbol)
    {
        if (HasExportAttribute(symbol) || HasInheritedExportAttribute(symbol))
        {
            return true;
        }

        foreach (ISymbol member in symbol.GetMembers())
        {
            if (member.IsStatic || member is ITypeSymbol)
            {
                continue;
            }

            if (HasExportAttribute(member))
            {
                return true;
            }
        }

        for (INamedTypeSymbol? baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (HasInheritedExportAttribute(baseType))
            {
                return true;
            }
        }

        return symbol.AllInterfaces.Any(HasInheritedExportAttribute);
    }

    internal static bool HasPartNotDiscoverableAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes().Any(attribute => IsPartNotDiscoverableAttribute(attribute.AttributeClass));
    }

    internal static AttributeData? GetImportAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.FirstOrDefault(attr => IsImportAttribute(attr.AttributeClass));
    }

    internal static bool GetAllowDefaultValue(AttributeData importAttribute)
    {
        KeyValuePair<string, TypedConstant> allowDefaultArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowDefault");
        return allowDefaultArg.Key is not null && allowDefaultArg.Value.Value is bool allowDefault && allowDefault;
    }

    internal static ISymbol? GetAffectedMemberSymbol(SuppressionAnalysisContext context, Diagnostic diagnostic)
    {
        foreach (Location location in diagnostic.AdditionalLocations)
        {
            ISymbol? symbol = GetDeclaredSymbol(context, location);
            if (symbol is IFieldSymbol or IPropertySymbol)
            {
                return symbol;
            }
        }

        return GetDeclaredSymbol(context, diagnostic.Location);
    }

    private static bool HasExportAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(attribute => IsExportAttribute(attribute.AttributeClass));
    }

    private static bool HasInheritedExportAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(attribute => IsInheritedExportAttribute(attribute.AttributeClass));
    }

    private static bool IsExportAttribute(INamedTypeSymbol? attributeType)
    {
        return attributeType is not null &&
            (IsAttributeOfType(attributeType, "ExportAttribute", MefV1AttributeNamespace.AsSpan()) ||
             IsAttributeOfType(attributeType, "ExportAttribute", MefV2AttributeNamespace.AsSpan()));
    }

    private static bool IsInheritedExportAttribute(INamedTypeSymbol? attributeType)
    {
        return attributeType is not null &&
            IsAttributeOfType(attributeType, "InheritedExportAttribute", MefV1AttributeNamespace.AsSpan());
    }

    private static bool IsPartNotDiscoverableAttribute(INamedTypeSymbol? attributeType)
    {
        return attributeType is not null &&
            (IsAttributeOfType(attributeType, "PartNotDiscoverableAttribute", MefV1AttributeNamespace.AsSpan()) ||
             IsAttributeOfType(attributeType, "PartNotDiscoverableAttribute", MefV2AttributeNamespace.AsSpan()));
    }

    private static bool IsImportAttribute(INamedTypeSymbol? attributeType)
    {
        return attributeType is not null &&
            (IsAttributeOfType(attributeType, "ImportAttribute", MefV1AttributeNamespace.AsSpan()) ||
             IsAttributeOfType(attributeType, "ImportAttribute", MefV2AttributeNamespace.AsSpan()) ||
             IsAttributeOfType(attributeType, "ImportManyAttribute", MefV1AttributeNamespace.AsSpan()) ||
             IsAttributeOfType(attributeType, "ImportManyAttribute", MefV2AttributeNamespace.AsSpan()));
    }

    private static bool IsAttributeOfType(INamedTypeSymbol attributeType, string attributeTypeName, ReadOnlySpan<string> expectedNamespace)
    {
        for (INamedTypeSymbol? current = attributeType; current is not null; current = current.BaseType)
        {
            if (current.Name == attributeTypeName && IsNamespaceMatch(current.ContainingNamespace, expectedNamespace))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNamespaceMatch(INamespaceSymbol? actual, ReadOnlySpan<string> expectedNames)
    {
        if (actual is null or { IsGlobalNamespace: true })
        {
            return expectedNames.Length == 0;
        }

        if (expectedNames.Length == 0 || actual.Name != expectedNames[expectedNames.Length - 1])
        {
            return false;
        }

        return IsNamespaceMatch(actual.ContainingNamespace, expectedNames[..^1]);
    }

    private static ISymbol? GetDeclaredSymbol(SuppressionAnalysisContext context, Location location)
    {
        SyntaxTree? syntaxTree = location.SourceTree;
        if (syntaxTree is null)
        {
            return null;
        }

        SemanticModel semanticModel = context.GetSemanticModel(syntaxTree);
        SyntaxNode root = syntaxTree.GetRoot(context.CancellationToken);
        SyntaxNode? node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

        while (node is not null)
        {
            ISymbol? symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
            if (symbol is not null)
            {
                return symbol;
            }

            node = node.Parent;
        }

        return null;
    }
}
