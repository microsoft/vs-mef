// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.CodeFixes;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Provides code fixes for VSMEF006 import nullability analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public class VSMEF006ImportNullabilityCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(VSMEF006ImportNullabilityAnalyzer.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (Diagnostic? diagnostic in context.Diagnostics.Where(d => this.FixableDiagnosticIds.Contains(d.Id)))
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
            SyntaxNode node = root.FindNode(diagnosticSpan);

            // Handle different node types (field, property, parameter)
            if (node is VariableDeclaratorSyntax variableDeclarator)
            {
                await RegisterFixesForField(context, root, diagnostic, variableDeclarator).ConfigureAwait(false);
            }
            else if (node is PropertyDeclarationSyntax property)
            {
                await RegisterFixesForProperty(context, root, diagnostic, property).ConfigureAwait(false);
            }
            else if (node is ParameterSyntax parameter)
            {
                await RegisterFixesForParameter(context, root, diagnostic, parameter).ConfigureAwait(false);
            }
        }
    }

    private static async Task RegisterFixesForField(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, VariableDeclaratorSyntax variableDeclarator)
    {
        FieldDeclarationSyntax? fieldDeclaration = variableDeclarator.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDeclaration is null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var fieldSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator, context.CancellationToken) as IFieldSymbol;
        if (fieldSymbol is null)
        {
            return;
        }

        AttributeData? importAttribute = GetImportAttribute(fieldSymbol.GetAttributes());
        if (importAttribute is null)
        {
            return;
        }

        RegisterFixes(context, root, diagnostic, fieldDeclaration, fieldDeclaration.Declaration.Type, fieldDeclaration.AttributeLists, importAttribute);
    }

    private static async Task RegisterFixesForProperty(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, PropertyDeclarationSyntax property)
    {
        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var propertySymbol = semanticModel.GetDeclaredSymbol(property, context.CancellationToken) as IPropertySymbol;
        if (propertySymbol is null)
        {
            return;
        }

        AttributeData? importAttribute = GetImportAttribute(propertySymbol.GetAttributes());
        if (importAttribute is null)
        {
            return;
        }

        RegisterFixes(context, root, diagnostic, property, property.Type, property.AttributeLists, importAttribute);
    }

    private static async Task RegisterFixesForParameter(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, ParameterSyntax parameter)
    {
        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) as IParameterSymbol;
        if (parameterSymbol is null)
        {
            return;
        }

        AttributeData? importAttribute = GetImportAttribute(parameterSymbol.GetAttributes());
        if (importAttribute is null)
        {
            return;
        }

        RegisterFixes(context, root, diagnostic, parameter, parameter.Type, parameter.AttributeLists, importAttribute);
    }

    private static void RegisterFixes(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, SyntaxNode targetNode, TypeSyntax? typeSyntax, SyntaxList<AttributeListSyntax> attributeLists, AttributeData importAttribute)
    {
        if (typeSyntax is null)
        {
            return;
        }

        bool isNullable = IsNullableTypeSyntax(typeSyntax);
        bool hasAllowDefault = GetAllowDefaultValue(importAttribute);

        if (diagnostic.Descriptor == VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
        {
            // Nullable type but no AllowDefault = true
            // Fix 1: Add AllowDefault = true
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add AllowDefault = true",
                    createChangedDocument: cancellationToken => AddAllowDefaultAsync(context.Document, root, targetNode, attributeLists, cancellationToken),
                    equivalenceKey: "AddAllowDefault"),
                diagnostic);

            // Fix 2: Make type non-nullable
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make type non-nullable",
                    createChangedDocument: cancellationToken => MakeTypeNonNullableAsync(context.Document, root, targetNode, typeSyntax, cancellationToken),
                    equivalenceKey: "MakeNonNullable"),
                diagnostic);
        }
        else if (diagnostic.Descriptor == VSMEF006ImportNullabilityAnalyzer.AllowDefaultWithoutNullableDescriptor)
        {
            // AllowDefault = true but not nullable
            // Fix 1: Make type nullable
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make type nullable",
                    createChangedDocument: cancellationToken => MakeTypeNullableAsync(context.Document, root, targetNode, typeSyntax, cancellationToken),
                    equivalenceKey: "MakeNullable"),
                diagnostic);

            // Fix 2: Remove AllowDefault = true
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove AllowDefault = true",
                    createChangedDocument: cancellationToken => RemoveAllowDefaultAsync(context.Document, root, targetNode, attributeLists, cancellationToken),
                    equivalenceKey: "RemoveAllowDefault"),
                diagnostic);
        }
    }

    private static bool IsNullableTypeSyntax(TypeSyntax typeSyntax)
    {
        return typeSyntax is NullableTypeSyntax;
    }

    private static AttributeData? GetImportAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.FirstOrDefault(attr => IsImportAttribute(attr.AttributeClass));
    }

    private static bool IsImportAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        string name = attributeType.Name;
        return name == "ImportAttribute" || name == "ImportManyAttribute";
    }

    private static bool GetAllowDefaultValue(AttributeData importAttribute)
    {
        KeyValuePair<string, TypedConstant> allowDefaultArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowDefault");
        if (allowDefaultArg.Key is not null && allowDefaultArg.Value.Value is bool allowDefault)
        {
            return allowDefault;
        }

        return false;
    }

    private static Task<Document> AddAllowDefaultAsync(Document document, SyntaxNode root, SyntaxNode targetNode, SyntaxList<AttributeListSyntax> attributeLists, CancellationToken cancellationToken)
    {
        AttributeListSyntax? importAttributeList = FindImportAttributeList(attributeLists);
        if (importAttributeList is null)
        {
            return Task.FromResult(document);
        }

        AttributeSyntax? importAttribute = FindImportAttribute(importAttributeList);
        if (importAttribute is null)
        {
            return Task.FromResult(document);
        }

        AttributeSyntax newAttribute = AddAllowDefaultToAttribute(importAttribute);
        AttributeListSyntax newAttributeList = importAttributeList.ReplaceNode(importAttribute, newAttribute);
        SyntaxNode newRoot = root.ReplaceNode(importAttributeList, newAttributeList);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> RemoveAllowDefaultAsync(Document document, SyntaxNode root, SyntaxNode targetNode, SyntaxList<AttributeListSyntax> attributeLists, CancellationToken cancellationToken)
    {
        AttributeListSyntax? importAttributeList = FindImportAttributeList(attributeLists);
        if (importAttributeList is null)
        {
            return Task.FromResult(document);
        }

        AttributeSyntax? importAttribute = FindImportAttribute(importAttributeList);
        if (importAttribute is null)
        {
            return Task.FromResult(document);
        }

        AttributeSyntax newAttribute = RemoveAllowDefaultFromAttribute(importAttribute);
        AttributeListSyntax newAttributeList = importAttributeList.ReplaceNode(importAttribute, newAttribute);
        SyntaxNode newRoot = root.ReplaceNode(importAttributeList, newAttributeList);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> MakeTypeNullableAsync(Document document, SyntaxNode root, SyntaxNode targetNode, TypeSyntax typeSyntax, CancellationToken cancellationToken)
    {
        NullableTypeSyntax nullableType = SyntaxFactory.NullableType(typeSyntax).WithTriviaFrom(typeSyntax);
        SyntaxNode newRoot = root.ReplaceNode(typeSyntax, nullableType);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> MakeTypeNonNullableAsync(Document document, SyntaxNode root, SyntaxNode targetNode, TypeSyntax typeSyntax, CancellationToken cancellationToken)
    {
        if (typeSyntax is NullableTypeSyntax nullableType)
        {
            TypeSyntax nonNullableType = nullableType.ElementType.WithTriviaFrom(typeSyntax);
            SyntaxNode newRoot = root.ReplaceNode(typeSyntax, nonNullableType);

            // For properties, add the '= null!' initializer to avoid CS8618 (uninitialized non-nullable property)
            if (targetNode is PropertyDeclarationSyntax property)
            {
                PropertyDeclarationSyntax newProperty = (PropertyDeclarationSyntax)newRoot.FindNode(property.Span);

                // Add '= null!' initializer if not already present
                if (newProperty.Initializer is null)
                {
                    // Remove trailing trivia from accessor list to keep initializer on same line
                    PropertyDeclarationSyntax propertyWithoutTrailingTrivia = newProperty;
                    if (newProperty.AccessorList is not null)
                    {
                        AccessorListSyntax accessorList = newProperty.AccessorList;
                        SyntaxToken closeBrace = accessorList.CloseBraceToken.WithTrailingTrivia();
                        accessorList = accessorList.WithCloseBraceToken(closeBrace);
                        propertyWithoutTrailingTrivia = newProperty.WithAccessorList(accessorList);
                    }

                    EqualsValueClauseSyntax initializer = SyntaxFactory.EqualsValueClause(
                        SyntaxFactory.PostfixUnaryExpression(
                            SyntaxKind.SuppressNullableWarningExpression,
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)))
                        .WithLeadingTrivia(SyntaxFactory.Space);

                    PropertyDeclarationSyntax updatedProperty = propertyWithoutTrailingTrivia
                        .WithInitializer(initializer)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                    newRoot = newRoot.ReplaceNode(newProperty, updatedProperty);
                }
            }

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        return Task.FromResult(document);
    }

    private static AttributeListSyntax? FindImportAttributeList(SyntaxList<AttributeListSyntax> attributeLists)
    {
        return attributeLists.FirstOrDefault(list => list.Attributes.Any(attr => IsImportAttributeSyntax(attr)));
    }

    private static AttributeSyntax? FindImportAttribute(AttributeListSyntax attributeList)
    {
        return attributeList.Attributes.FirstOrDefault(attr => IsImportAttributeSyntax(attr));
    }

    private static bool IsImportAttributeSyntax(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString();
        return name.Contains("Import") && (name.Contains("ImportAttribute") || name.Contains("Import"));
    }

    private static AttributeSyntax AddAllowDefaultToAttribute(AttributeSyntax attribute)
    {
        AttributeArgumentSyntax allowDefaultArg = SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("AllowDefault")),
            null,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));

        if (attribute.ArgumentList is null)
        {
            return attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(allowDefaultArg)));
        }

        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
        int allowDefaultIndex = -1;

        // Check if AllowDefault already exists
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameEquals?.Name.Identifier.ValueText == "AllowDefault")
            {
                allowDefaultIndex = i;
                break;
            }
        }

        if (allowDefaultIndex >= 0)
        {
            // Replace existing AllowDefault
            SeparatedSyntaxList<AttributeArgumentSyntax> newArguments = arguments.Replace(arguments[allowDefaultIndex], allowDefaultArg);
            return attribute.WithArgumentList(attribute.ArgumentList.WithArguments(newArguments));
        }
        else
        {
            // Add new AllowDefault
            SeparatedSyntaxList<AttributeArgumentSyntax> newArguments = arguments.Add(allowDefaultArg);
            return attribute.WithArgumentList(attribute.ArgumentList.WithArguments(newArguments));
        }
    }

    private static AttributeSyntax RemoveAllowDefaultFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return attribute;
        }

        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
        int allowDefaultIndex = -1;

        // Find AllowDefault argument
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameEquals?.Name.Identifier.ValueText == "AllowDefault")
            {
                allowDefaultIndex = i;
                break;
            }
        }

        if (allowDefaultIndex >= 0)
        {
            SeparatedSyntaxList<AttributeArgumentSyntax> newArguments = arguments.RemoveAt(allowDefaultIndex);
            if (newArguments.Count == 0)
            {
                return attribute.WithArgumentList(null);
            }
            else
            {
                return attribute.WithArgumentList(attribute.ArgumentList.WithArguments(newArguments));
            }
        }

        return attribute;
    }
}
