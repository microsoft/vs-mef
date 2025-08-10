// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.CodeFixes;

using System;
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

/// <summary>
/// Provides code fixes for VSMEF006 import nullability analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VSMEF006ImportNullabilityCodeFixProvider))]
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
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics.Where(d => this.FixableDiagnosticIds.Contains(d.Id)))
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

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
        var fieldDeclaration = variableDeclarator.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDeclaration is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var fieldSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator, context.CancellationToken) as IFieldSymbol;
        if (fieldSymbol is null)
        {
            return;
        }

        var importAttribute = GetImportAttribute(fieldSymbol.GetAttributes());
        if (importAttribute is null)
        {
            return;
        }

        RegisterFixes(context, root, diagnostic, fieldDeclaration, fieldDeclaration.Declaration.Type, fieldDeclaration.AttributeLists, importAttribute);
    }

    private static async Task RegisterFixesForProperty(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, PropertyDeclarationSyntax property)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var propertySymbol = semanticModel.GetDeclaredSymbol(property, context.CancellationToken) as IPropertySymbol;
        if (propertySymbol is null)
        {
            return;
        }

        var importAttribute = GetImportAttribute(propertySymbol.GetAttributes());
        if (importAttribute is null)
        {
            return;
        }

        RegisterFixes(context, root, diagnostic, property, property.Type, property.AttributeLists, importAttribute);
    }

    private static async Task RegisterFixesForParameter(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic, ParameterSyntax parameter)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) as IParameterSymbol;
        if (parameterSymbol is null)
        {
            return;
        }

        var importAttribute = GetImportAttribute(parameterSymbol.GetAttributes());
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

        var name = attributeType.Name;
        return name == "ImportAttribute" || name == "ImportManyAttribute";
    }

    private static bool GetAllowDefaultValue(AttributeData importAttribute)
    {
        var allowDefaultArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowDefault");
        if (allowDefaultArg.Key is not null && allowDefaultArg.Value.Value is bool allowDefault)
        {
            return allowDefault;
        }

        return false;
    }

    private static Task<Document> AddAllowDefaultAsync(Document document, SyntaxNode root, SyntaxNode targetNode, SyntaxList<AttributeListSyntax> attributeLists, CancellationToken cancellationToken)
    {
        var importAttributeList = FindImportAttributeList(attributeLists);
        if (importAttributeList is null)
        {
            return Task.FromResult(document);
        }

        var importAttribute = FindImportAttribute(importAttributeList);
        if (importAttribute is null)
        {
            return Task.FromResult(document);
        }

        var newAttribute = AddAllowDefaultToAttribute(importAttribute);
        var newAttributeList = importAttributeList.ReplaceNode(importAttribute, newAttribute);
        var newRoot = root.ReplaceNode(importAttributeList, newAttributeList);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> RemoveAllowDefaultAsync(Document document, SyntaxNode root, SyntaxNode targetNode, SyntaxList<AttributeListSyntax> attributeLists, CancellationToken cancellationToken)
    {
        var importAttributeList = FindImportAttributeList(attributeLists);
        if (importAttributeList is null)
        {
            return Task.FromResult(document);
        }

        var importAttribute = FindImportAttribute(importAttributeList);
        if (importAttribute is null)
        {
            return Task.FromResult(document);
        }

        var newAttribute = RemoveAllowDefaultFromAttribute(importAttribute);
        var newAttributeList = importAttributeList.ReplaceNode(importAttribute, newAttribute);
        var newRoot = root.ReplaceNode(importAttributeList, newAttributeList);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> MakeTypeNullableAsync(Document document, SyntaxNode root, SyntaxNode targetNode, TypeSyntax typeSyntax, CancellationToken cancellationToken)
    {
        var nullableType = SyntaxFactory.NullableType(typeSyntax).WithTriviaFrom(typeSyntax);
        var newRoot = root.ReplaceNode(typeSyntax, nullableType);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> MakeTypeNonNullableAsync(Document document, SyntaxNode root, SyntaxNode targetNode, TypeSyntax typeSyntax, CancellationToken cancellationToken)
    {
        if (typeSyntax is NullableTypeSyntax nullableType)
        {
            var nonNullableType = nullableType.ElementType.WithTriviaFrom(typeSyntax);
            var newRoot = root.ReplaceNode(typeSyntax, nonNullableType);
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
        var name = attribute.Name.ToString();
        return name.Contains("Import") && (name.Contains("ImportAttribute") || name.Contains("Import"));
    }

    private static AttributeSyntax AddAllowDefaultToAttribute(AttributeSyntax attribute)
    {
        var allowDefaultArg = SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("AllowDefault")),
            null,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));

        if (attribute.ArgumentList is null)
        {
            return attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(allowDefaultArg)));
        }

        var arguments = attribute.ArgumentList.Arguments;
        var allowDefaultIndex = -1;

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
            var newArguments = arguments.Replace(arguments[allowDefaultIndex], allowDefaultArg);
            return attribute.WithArgumentList(attribute.ArgumentList.WithArguments(newArguments));
        }
        else
        {
            // Add new AllowDefault
            var newArguments = arguments.Add(allowDefaultArg);
            return attribute.WithArgumentList(attribute.ArgumentList.WithArguments(newArguments));
        }
    }

    private static AttributeSyntax RemoveAllowDefaultFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return attribute;
        }

        var arguments = attribute.ArgumentList.Arguments;
        var allowDefaultIndex = -1;

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
            var newArguments = arguments.RemoveAt(allowDefaultIndex);
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
