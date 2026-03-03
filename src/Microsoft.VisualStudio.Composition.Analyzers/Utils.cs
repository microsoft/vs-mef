// Copyright (c) Microsoft Corporation. All rights reserved.
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
        return $"https://microsoft.github.io/vs-mef/analyzers/{analyzerId}.html";
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
    /// Determines whether the specified attribute type is a MEF InheritedExport attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is an inherited export attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for MEF v1's InheritedExportAttribute, or a subtype of that attribute.
    /// </remarks>
    internal static bool IsInheritedExportAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check if it's InheritedExportAttribute from MEF v1
        if (IsAttributeOfType(attributeType, "InheritedExportAttribute", MefV1AttributeNamespace.AsSpan()))
        {
            return true;
        }

        return false;
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
    /// Determines whether the specified attribute type is a MEF PartNotDiscoverable attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is a PartNotDiscoverable attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for both MEF v1 and MEF v2 PartNotDiscoverable attributes.
    /// </remarks>
    internal static bool IsPartNotDiscoverableAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check if it's a PartNotDiscoverable attribute (not typically subclassed, but check inheritance for consistency)
        return IsAttributeOfType(attributeType, "PartNotDiscoverableAttribute", MefV1AttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "PartNotDiscoverableAttribute", MefV2AttributeNamespace.AsSpan());
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
        INamedTypeSymbol? current = attributeType;
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
    /// It also checks for InheritedExport attributes on base classes and interfaces.
    /// Static members with export attributes are ignored since they don't require type instantiation.
    /// </remarks>
    internal static bool HasInstanceExports(INamedTypeSymbol symbol)
    {
        // Check the type itself for Export attributes
        if (HasExportAttribute(symbol))
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

            if (HasExportAttribute(member))
            {
                return true;
            }
        }

        // Check base classes for InheritedExport attributes
        INamedTypeSymbol? baseType = symbol.BaseType;
        while (baseType is not null)
        {
            foreach (AttributeData attribute in baseType.GetAttributes())
            {
                if (IsInheritedExportAttribute(attribute.AttributeClass))
                {
                    return true;
                }
            }

            baseType = baseType.BaseType;
        }

        // Check interfaces for InheritedExport attributes
        foreach (INamedTypeSymbol interfaceType in symbol.AllInterfaces)
        {
            foreach (AttributeData attribute in interfaceType.GetAttributes())
            {
                if (IsInheritedExportAttribute(attribute.AttributeClass))
                {
                    return true;
                }
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
    /// Determines whether the specified type has the PartNotDiscoverable attribute.
    /// </summary>
    /// <param name="symbol">The type symbol to check.</param>
    /// <returns><see langword="true"/> if the type has the PartNotDiscoverable attribute; otherwise, <see langword="false"/>.</returns>
    internal static bool HasPartNotDiscoverableAttribute(INamedTypeSymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (IsPartNotDiscoverableAttribute(attribute.AttributeClass))
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

    /// <summary>
    /// Gets explicit contract name and type values from an import/export-style attribute.
    /// </summary>
    /// <remarks>
    /// Supports positional and named arguments used by MEF attributes:
    /// <c>(string contractName)</c>, <c>(Type contractType)</c>, and <c>(string contractName, Type contractType)</c>.
    /// Empty contract names are treated as unspecified.
    /// </remarks>
    internal static (string? ExplicitContractName, INamedTypeSymbol? ExplicitContractType) GetExplicitContractInfo(AttributeData attribute)
    {
        (string? explicitContractName, INamedTypeSymbol? explicitContractType) = attribute.ConstructorArguments switch
        {
            [TypedConstant { Value: string { Length: not 0 } contractName }] => (contractName, null),
            [TypedConstant { Value: INamedTypeSymbol contractType }] => (null, contractType),
            [TypedConstant { Value: string { Length: not 0 } contractName }, TypedConstant { Value: INamedTypeSymbol contractType }] => (contractName, contractType),
            [TypedConstant { Value: null or string { Length: 0 } }, TypedConstant { Value: INamedTypeSymbol contractType }] => (null, contractType),
            _ => (null, null),
        };

        TypedConstant? nameArg = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "ContractName").Value;
        if (nameArg?.Value is string { Length: not 0 } namedContractName)
        {
            explicitContractName = namedContractName;
        }

        TypedConstant? typeArg = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "ContractType").Value;
        if (typeArg?.Value is INamedTypeSymbol namedContractType)
        {
            explicitContractType = namedContractType;
        }

        return (explicitContractName, explicitContractType);
    }

    /// <summary>
    /// Computes MEF contract name from explicit contract metadata.
    /// </summary>
    internal static string GetMefContractName(string? explicitContractName, ITypeSymbol contractType)
    {
        if (explicitContractName is { Length: > 0 })
        {
            return explicitContractName;
        }

        return MefContractNameServices.GetTypeIdentity(contractType);
    }

    internal static bool GetAllowDefaultValue(AttributeData importAttribute)
    {
        KeyValuePair<string, TypedConstant> allowDefaultArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowDefault");
        if (allowDefaultArg.Key is not null && allowDefaultArg.Value.Value is bool allowDefault)
        {
            return allowDefault;
        }

        return false; // Default value is false
    }

    /// <summary>
    /// Determines whether an Import attribute requires a NonShared instance.
    /// </summary>
    /// <param name="importAttribute">The Import attribute to check.</param>
    /// <returns>True if RequiredCreationPolicy = NonShared; otherwise false.</returns>
    internal static bool IsNonSharedImport(AttributeData importAttribute)
    {
        TypedConstant creationPolicyArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "RequiredCreationPolicy").Value;

        // CreationPolicy values:
        //     Any = 0
        //     Shared = 1
        //     NonShared = 2
        return creationPolicyArg.Value is 2;
    }

    /// <summary>
    /// Determines which MEF version an attribute type belongs to by walking up its inheritance hierarchy.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns>
    /// <see cref="MefVersion.V1"/> if the attribute belongs to MEF v1 (System.ComponentModel.Composition);
    /// <see cref="MefVersion.V2"/> if the attribute belongs to MEF v2 (System.Composition);
    /// <see langword="null"/> if the attribute does not belong to either MEF namespace.
    /// </returns>
    /// <remarks>
    /// This method walks up the inheritance hierarchy to handle custom attributes that derive from MEF attributes.
    /// For example, a custom export attribute derived from System.ComponentModel.Composition.ExportAttribute
    /// will be correctly identified as MEF v1 even if it lives in a different namespace.
    /// </remarks>
    internal static MefVersion? GetMefVersionFromAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return null;
        }

        // Walk up the inheritance hierarchy to find the base MEF attribute
        // This handles custom export attributes that derive from ExportAttribute
        INamedTypeSymbol? current = attributeType;
        while (current is not null)
        {
            if (IsNamespaceMatch(current.ContainingNamespace, MefV1AttributeNamespace.AsSpan()))
            {
                return MefVersion.V1;
            }

            if (IsNamespaceMatch(current.ContainingNamespace, MefV2AttributeNamespace.AsSpan()))
            {
                return MefVersion.V2;
            }

            current = current.BaseType;
        }

        return null;
    }

    internal static void Deconstruct<TKey, TValue>(this IGrouping<TKey, TValue> grouping, out TKey key, out IEnumerable<TValue> values)
    {
        key = grouping.Key;
        values = grouping;
    }
}

/// <summary>
/// Represents the version of MEF (Managed Extensibility Framework) being used.
/// </summary>
internal enum MefVersion
{
    /// <summary>
    /// MEF v1 (System.ComponentModel.Composition).
    /// </summary>
    V1,

    /// <summary>
    /// MEF v2 (System.Composition).
    /// </summary>
    V2,
}
