﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

/// <summary>
/// Utility methods for analyzers.
/// </summary>
internal static class Utils
{
    /// <summary>
    /// Gets the URL to the help topic for a particular analyzer.
    /// </summary>
    /// <param name="analyzerId">The ID of the analyzer.</param>
    /// <returns>The URL for the analyzer's documentation.</returns>
    internal static string GetHelpLink(string analyzerId)
    {
        return $"https://github.com/Microsoft/vs-mef/blob/main/doc/analyzers/{analyzerId}.md";
    }

    internal static bool ReferencesMefAttributes(Compilation compilation)
    {
        return compilation.ReferencedAssemblyNames.Any(i =>
            string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Name, "System.Composition.AttributedModel", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly ImmutableArray<string> MefV1AttributeNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> MefV2AttributeNamespace = ImmutableArray.Create("System", "Composition");

    /// <summary>
    /// Determines whether the specified attribute type is a MEF export attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is an export attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for both MEF v1 and MEF v2 export attributes, including custom attributes that derive from the base export attribute types.
    /// </remarks>
    internal static bool IsExportAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check if it's a direct Export attribute or derived from one
        return IsAttributeOfType(attributeType, "ExportAttribute", MefV1AttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ExportAttribute", MefV2AttributeNamespace.AsSpan());
    }

    /// <summary>
    /// Determines whether the specified attribute type is a MEF importing constructor attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is an importing constructor attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for both MEF v1 and MEF v2 importing constructor attributes, including custom attributes that derive from the base importing constructor attribute types.
    /// </remarks>
    internal static bool IsImportingConstructorAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check if it's a direct ImportingConstructor attribute or derived from one
        return IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV1AttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV2AttributeNamespace.AsSpan());
    }

    /// <summary>
    /// Determines whether the specified attribute type matches the expected type and namespace, including inheritance hierarchy.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <param name="attributeTypeName">The expected attribute type name.</param>
    /// <param name="expectedNamespace">The expected namespace components.</param>
    /// <returns><see langword="true"/> if the attribute type matches; otherwise, <see langword="false"/>.</returns>
    private static bool IsAttributeOfType(INamedTypeSymbol attributeType, string attributeTypeName, ReadOnlySpan<string> expectedNamespace)
    {
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
    /// Determines whether the specified symbol has any MEF export attributes. Members of the symbol (if any) are <em>not</em> checked.
    /// </summary>
    /// <param name="symbol">The symbol to check for export attributes.</param>
    /// <returns><see langword="true"/> if the symbol has export attributes; otherwise, <see langword="false"/>.</returns>
    internal static bool HasExportAttribute(ISymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (IsExportAttribute(attribute.AttributeClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified type has any instance-level MEF exports.
    /// </summary>
    /// <param name="symbol">The type symbol to analyze.</param>
    /// <returns><see langword="true"/> if the type has instance exports; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for export attributes on the type itself or on its instance members (properties, methods, fields).
    /// Static members with export attributes are ignored since they don't require type instantiation.
    /// </remarks>
    internal static bool HasInstanceExports(INamedTypeSymbol symbol)
    {
        // Check the type itself for Export attributes
        if (Utils.HasExportAttribute(symbol))
        {
            return true;
        }

        // Check instance members (properties, methods, fields) for Export attributes
        foreach (ISymbol member in symbol.GetMembers())
        {
            // Skip static members and nested types
            if (member.IsStatic || member is ITypeSymbol)
            {
                continue;
            }

            if (Utils.HasExportAttribute(member))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified constructor has the ImportingConstructor attribute.
    /// </summary>
    /// <param name="constructor">The constructor to check.</param>
    /// <returns><see langword="true"/> if the constructor has the ImportingConstructor attribute; otherwise, <see langword="false"/>.</returns>
    internal static bool HasImportingConstructorAttribute(IMethodSymbol constructor)
    {
        foreach (AttributeData attribute in constructor.GetAttributes())
        {
            if (IsImportingConstructorAttribute(attribute.AttributeClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified namespace matches the expected namespace components.
    /// </summary>
    /// <param name="actual">The actual namespace to check.</param>
    /// <param name="expectedNames">The expected namespace components in reverse order (leaf to root).</param>
    /// <returns><see langword="true"/> if the namespace matches; otherwise, <see langword="false"/>.</returns>
    internal static bool IsNamespaceMatch(INamespaceSymbol? actual, ReadOnlySpan<string> expectedNames)
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

    internal static AttributeData? GetImportAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.FirstOrDefault(attr => IsImportAttribute(attr.AttributeClass));
    }

    internal static bool IsImportAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        return IsAttributeOfType(attributeType, "ImportAttribute", MefV1AttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportAttribute", MefV2AttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportManyAttribute", MefV1AttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportManyAttribute", MefV2AttributeNamespace.AsSpan());
    }

    internal static bool GetAllowDefaultValue(AttributeData importAttribute)
    {
        var allowDefaultArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowDefault");
        if (allowDefaultArg.Key is not null && allowDefaultArg.Value.Value is bool allowDefault)
        {
            return allowDefault;
        }

        return false; // Default value is false
    }
}
