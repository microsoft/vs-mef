// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.CSharp;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class MetadataViewGeneratorUtilities
{
    private static readonly ImmutableArray<string> MefV1AttributeNamespace = ["System", "ComponentModel", "Composition"];
    private static readonly ImmutableArray<string> MefV2AttributeNamespace = ["System", "Composition"];

    internal static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == attributeFullName);
    }

    internal static AttributeData? GetImportAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.FirstOrDefault(attr => IsImportAttribute(attr.AttributeClass));
    }

    internal static bool IsImportManyAttribute(INamedTypeSymbol? attributeType)
    {
        return IsAttributeOfType(attributeType, "ImportManyAttribute", MefV1AttributeNamespace)
            || IsAttributeOfType(attributeType, "ImportManyAttribute", MefV2AttributeNamespace);
    }

    internal static bool HasImportingConstructorAttribute(IMethodSymbol constructor)
    {
        return constructor.GetAttributes().Any(attribute =>
            IsAttributeOfType(attribute.AttributeClass, Types.ImportingConstructorAttribute.Name, MefV1AttributeNamespace)
            || IsAttributeOfType(attribute.AttributeClass, Types.ImportingConstructorAttributeV2.Name, MefV2AttributeNamespace));
    }

    internal static bool TryGetMetadataViewInterface(ITypeSymbol memberType, bool isImportMany, [NotNullWhen(true)] out INamedTypeSymbol? metadataViewInterface)
    {
        ITypeSymbol candidateType = isImportMany ? GetCollectionElementType(memberType) ?? memberType : memberType;
        if (candidateType is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && IsMetadataCarrierWithMetadataType(namedType)
            && namedType.TypeArguments[1] is INamedTypeSymbol metadataInterface
            && metadataInterface.TypeKind == TypeKind.Interface
            && !IsBuiltInMetadataViewInterface(metadataInterface))
        {
            metadataViewInterface = metadataInterface;
            return true;
        }

        metadataViewInterface = null;
        return false;
    }

    internal static bool IsGeneratableMetadataViewInterface(INamedTypeSymbol metadataViewInterface)
    {
        foreach (INamedTypeSymbol interfaceType in new[] { metadataViewInterface }.Concat(metadataViewInterface.AllInterfaces))
        {
            foreach (ISymbol member in interfaceType.GetMembers())
            {
                if (member is IPropertySymbol { IsIndexer: false, GetMethod: not null, SetMethod: null })
                {
                    continue;
                }

                if (member is IMethodSymbol { AssociatedSymbol: IPropertySymbol })
                {
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    internal static bool IsPartialInterfaceAndContainingTypes(INamedTypeSymbol metadataViewInterface, CancellationToken cancellationToken)
    {
        return metadataViewInterface.DeclaringSyntaxReferences.Length > 0
            && metadataViewInterface.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<InterfaceDeclarationSyntax>()
                .All(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            && GetContainingTypeDeclarations(metadataViewInterface, cancellationToken)
                .All(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    internal static Location GetFirstInvalidMetadataViewLocation(INamedTypeSymbol metadataViewInterface, CancellationToken cancellationToken)
    {
        InterfaceDeclarationSyntax? nonPartialInterfaceDeclaration = metadataViewInterface.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<InterfaceDeclarationSyntax>()
            .FirstOrDefault(static declaration => !declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        if (nonPartialInterfaceDeclaration is not null)
        {
            return nonPartialInterfaceDeclaration.Identifier.GetLocation();
        }

        TypeDeclarationSyntax? nonPartialContainingTypeDeclaration = GetContainingTypeDeclarations(metadataViewInterface, cancellationToken)
            .FirstOrDefault(static declaration => !declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        if (nonPartialContainingTypeDeclaration is not null)
        {
            return nonPartialContainingTypeDeclaration.Identifier.GetLocation();
        }

        return metadataViewInterface.Locations.First();
    }

    private static bool IsImportAttribute(INamedTypeSymbol? attributeType)
    {
        return IsAttributeOfType(attributeType, Types.ImportAttribute.Name, MefV1AttributeNamespace)
            || IsAttributeOfType(attributeType, Types.ImportAttributeV2.Name, MefV2AttributeNamespace)
            || IsImportManyAttribute(attributeType);
    }

    private static bool IsAttributeOfType(INamedTypeSymbol? attributeType, string attributeTypeName, ImmutableArray<string> expectedNamespace)
    {
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

    private static bool IsNamespaceMatch(INamespaceSymbol? actual, ImmutableArray<string> expectedNames)
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

        return IsNamespaceMatch(actual.ContainingNamespace, expectedNames.RemoveAt(expectedNames.Length - 1));
    }

    private static IEnumerable<TypeDeclarationSyntax> GetContainingTypeDeclarations(INamedTypeSymbol metadataViewInterface, CancellationToken cancellationToken)
    {
        for (INamedTypeSymbol? containingType = metadataViewInterface.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            foreach (TypeDeclarationSyntax declaration in containingType.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<TypeDeclarationSyntax>())
            {
                yield return declaration;
            }
        }
    }

    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol collectionType)
    {
        if (collectionType is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        if (collectionType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (INamedTypeSymbol candidate in namedType.AllInterfaces.Concat(new[] { namedType }))
            {
                if (candidate.IsGenericType
                    && candidate.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
                    && candidate.Name == "IEnumerable")
                {
                    return candidate.TypeArguments[0];
                }
            }
        }

        return null;
    }

    private static bool IsMetadataCarrierWithMetadataType(INamedTypeSymbol namedType)
    {
        INamedTypeSymbol genericType = namedType.OriginalDefinition;
        string containingNamespace = genericType.ContainingNamespace.ToDisplayString();
        return (genericType.MetadataName == Types.Lazy.Name && containingNamespace == "System")
            || (genericType.MetadataName == Types.ExportFactory.Name
                && (containingNamespace == "System.ComponentModel.Composition" || containingNamespace == "System.Composition"));
    }

    private static bool IsBuiltInMetadataViewInterface(INamedTypeSymbol metadataInterface)
    {
        INamedTypeSymbol originalDefinition = metadataInterface.OriginalDefinition;
        return metadataInterface.TypeArguments.Length == 2
            && originalDefinition.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
            && (originalDefinition.Name == "IDictionary" || originalDefinition.Name == "IReadOnlyDictionary")
            && metadataInterface.TypeArguments[0].SpecialType == SpecialType.System_String
            && metadataInterface.TypeArguments[1].SpecialType == SpecialType.System_Object;
    }
}
