// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Produces MEF contract type identity strings from Roslyn symbols.
/// </summary>
/// <remarks>
/// This mirrors the stable contract naming algorithm used by vs-mef runtime code (ContractNameServices),
/// but operates on Roslyn symbols so analyzers can compute the same identities during compilation.
/// </remarks>
public static class MefContractNameServices
{
    private const char NamespaceSeparator = '.';
    private const char ArrayOpeningBracket = '[';
    private const char ArrayClosingBracket = ']';
    private const char ArraySeparator = ',';
    private const char PointerSymbol = '*';
    private const char ReferenceSymbol = '&';
    private const char GenericArityBackQuote = '`';
    private const char NestedClassSeparator = '+';
    private const char GenericOpeningBracket = '(';
    private const char GenericClosingBracket = ')';
    private const char GenericArgumentSeparator = ',';
    private const char GenericFormatOpeningBracket = '{';
    private const char GenericFormatClosingBracket = '}';

    /// <summary>
    /// Gets MEF type identity for a Roslyn type symbol.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <param name="formatGenericName">
    /// <see langword="true"/> to emit generic parameter placeholders as <c>{0}</c>, <c>{1}</c>, etc.
    /// </param>
    /// <returns>The contract type identity string.</returns>
    public static string GetTypeIdentity(ITypeSymbol type, bool formatGenericName = true)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Delegate, IsAbstract: false } delegateType &&
            delegateType.DelegateInvokeMethod is IMethodSymbol invokeMethod)
        {
            return GetTypeIdentityFromMethod(invokeMethod, formatGenericName);
        }

        var typeIdentity = new StringBuilder();
        WriteTypeWithNamespace(typeIdentity, type, formatGenericName);
        return typeIdentity.ToString();
    }

    /// <summary>
    /// Gets MEF type identity for a Roslyn method symbol.
    /// </summary>
    /// <param name="method">The method symbol.</param>
    /// <param name="formatGenericName">
    /// <see langword="true"/> to emit generic parameter placeholders as <c>{0}</c>, <c>{1}</c>, etc.
    /// </param>
    /// <returns>The contract method identity string.</returns>
    public static string GetTypeIdentityFromMethod(IMethodSymbol method, bool formatGenericName = true)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var methodName = new StringBuilder();
        WriteTypeWithNamespace(methodName, method.ReturnType, formatGenericName);
        methodName.Append(GenericOpeningBracket);

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
            {
                methodName.Append(GenericArgumentSeparator);
            }

            IParameterSymbol parameter = method.Parameters[i];
            WriteTypeWithNamespace(methodName, parameter.Type, formatGenericName);
            if (parameter.RefKind != RefKind.None)
            {
                methodName.Append(ReferenceSymbol);
            }
        }

        methodName.Append(GenericClosingBracket);
        return methodName.ToString();
    }

    private static void WriteTypeWithNamespace(StringBuilder typeName, ITypeSymbol type, bool formatGenericName)
    {
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            typeName.Append(ns.ToDisplayString());
            typeName.Append(NamespaceSeparator);
        }

        WriteType(typeName, type, formatGenericName);
    }

    private static void WriteType(StringBuilder typeName, ITypeSymbol type, bool formatGenericName)
    {
        switch (type)
        {
            case IArrayTypeSymbol arrayType:
                WriteArrayType(typeName, arrayType, formatGenericName);
                return;

            case IPointerTypeSymbol pointerType:
                WriteTypeWithNamespace(typeName, pointerType.PointedAtType, formatGenericName);
                typeName.Append(PointerSymbol);
                return;

            case INamedTypeSymbol namedType when namedType.IsGenericType:
                WriteGenericType(typeName, namedType, formatGenericName);
                return;

            case ITypeParameterSymbol typeParameter:
                WriteTypeParameter(typeName, typeParameter, formatGenericName);
                return;

            default:
                WriteNonGenericType(typeName, type, formatGenericName);
                return;
        }
    }

    private static void WriteNonGenericType(StringBuilder typeName, ITypeSymbol type, bool formatGenericName)
    {
        if (type is INamedTypeSymbol { ContainingType: not null } namedType)
        {
            WriteType(typeName, namedType.ContainingType, formatGenericName);
            typeName.Append(NestedClassSeparator);
        }

        if (type is INamedTypeSymbol named)
        {
            typeName.Append(StripGenericArity(named.MetadataName));
            return;
        }

        typeName.Append(type.Name);
    }

    private static void WriteArrayType(StringBuilder typeName, IArrayTypeSymbol type, bool formatGenericName)
    {
        // Jagged arrays are represented as nested array symbols. Emit dimensions from the outermost array
        // to preserve C#-style ordering.
        ITypeSymbol elementType = type;
        var ranks = new List<int>();
        while (elementType is IArrayTypeSymbol arrayType)
        {
            ranks.Add(arrayType.Rank);
            elementType = arrayType.ElementType;
        }

        WriteTypeWithNamespace(typeName, elementType, formatGenericName);

        foreach (int rank in ranks)
        {
            typeName.Append(ArrayOpeningBracket);
            for (int i = 1; i < rank; i++)
            {
                typeName.Append(ArraySeparator);
            }

            typeName.Append(ArrayClosingBracket);
        }
    }

    private static void WriteGenericType(StringBuilder typeName, INamedTypeSymbol type, bool formatGenericName)
    {
        if (type.ContainingType is not null)
        {
            WriteType(typeName, type.ContainingType, formatGenericName);
            typeName.Append(NestedClassSeparator);
        }

        typeName.Append(StripGenericArity(type.MetadataName));
        WriteTypeArguments(typeName, type, formatGenericName);
    }

    private static void WriteTypeArguments(StringBuilder typeName, INamedTypeSymbol type, bool formatGenericName)
    {
        typeName.Append(GenericOpeningBracket);
        for (int i = 0; i < type.TypeArguments.Length; i++)
        {
            if (i > 0)
            {
                typeName.Append(GenericArgumentSeparator);
            }

            ITypeSymbol argument = type.TypeArguments[i];

            if (argument is ITypeParameterSymbol typeParameter)
            {
                WriteTypeParameter(typeName, typeParameter, formatGenericName);
            }
            else
            {
                WriteTypeWithNamespace(typeName, argument, formatGenericName);
            }
        }

        typeName.Append(GenericClosingBracket);
    }

    private static void WriteTypeParameter(StringBuilder typeName, ITypeParameterSymbol typeParameter, bool formatGenericName)
    {
        if (!formatGenericName)
        {
            return;
        }

        typeName.Append(GenericFormatOpeningBracket);
        typeName.Append(GetGenericParameterPosition(typeParameter));
        typeName.Append(GenericFormatClosingBracket);
    }

    private static int GetGenericParameterPosition(ITypeParameterSymbol typeParameter)
    {
        if (typeParameter.TypeParameterKind == TypeParameterKind.Method)
        {
            return typeParameter.Ordinal;
        }

        int offset = 0;
        INamedTypeSymbol? containingType = typeParameter.ContainingType?.ContainingType;
        while (containingType is not null)
        {
            offset += containingType.Arity;
            containingType = containingType.ContainingType;
        }

        return offset + typeParameter.Ordinal;
    }

    private static string StripGenericArity(string metadataName)
    {
        int indexOfBackQuote = metadataName.IndexOf(GenericArityBackQuote);
        return indexOfBackQuote > -1 ? metadataName[..indexOfBackQuote] : metadataName;
    }
}
