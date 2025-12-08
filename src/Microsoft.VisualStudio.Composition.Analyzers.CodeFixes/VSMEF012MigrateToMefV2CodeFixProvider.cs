// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

/// <summary>
/// Provides a code fix that migrates MEFv1 attributes to MEFv2.
/// </summary>
/// <remarks>
/// <para>
/// This code fix only works for MEFv1 → MEFv2 migration because:
/// </para>
/// <list type="number">
/// <item>MEFv2 attributes have more restricted usage (e.g., can't be applied to fields).</item>
/// <item>The reverse migration would often fail or produce invalid code.</item>
/// </list>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VSMEF012MigrateToMefV2CodeFixProvider))]
[Shared]
public class VSMEF012MigrateToMefV2CodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(VSMEF012DisallowMefAttributeVersionAnalyzer.Id);

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

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            // Only offer fix for MEFv1 attributes (migrating to MEFv2)
            if (!diagnostic.Properties.TryGetValue("AttributeVersion", out string? version) || version != "V1")
            {
                continue;
            }

            SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);
            AttributeSyntax? attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
            if (attribute is null)
            {
                continue;
            }

            // Check if the target is a field - MEFv2 Import/ImportMany can't be applied to fields
            if (IsFieldAttribute(attribute))
            {
                // Don't offer a fix for fields since MEFv2 doesn't support them
                continue;
            }

            string attributeName = GetAttributeSimpleName(attribute);
            if (CanMigrateAttribute(attributeName))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.VSMEF012_CodeFix_MigrateToMefV2, attributeName),
                        createChangedDocument: ct => MigrateToMefV2Async(context.Document, attribute, ct),
                        equivalenceKey: $"MigrateToMefV2_{attributeName}"),
                    diagnostic);
            }
        }
    }

    private static bool IsFieldAttribute(AttributeSyntax attribute)
    {
        // Walk up to find if we're on a field declaration
        SyntaxNode? current = attribute.Parent;
        while (current is not null)
        {
            if (current is FieldDeclarationSyntax)
            {
                return true;
            }

            if (current is PropertyDeclarationSyntax or MethodDeclarationSyntax or ParameterSyntax or ClassDeclarationSyntax)
            {
                return false;
            }

            current = current.Parent;
        }

        return false;
    }

    private static string GetAttributeSimpleName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
            _ => string.Empty,
        };
    }

    private static bool CanMigrateAttribute(string attributeName)
    {
        // These MEFv1 attributes have direct MEFv2 equivalents
        return attributeName is "Import" or "ImportAttribute"
            or "ImportMany" or "ImportManyAttribute"
            or "Export" or "ExportAttribute"
            or "ImportingConstructor" or "ImportingConstructorAttribute";
    }

    private static async Task<Document> MigrateToMefV2Async(
        Document document,
        AttributeSyntax attribute,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Create the new attribute with MEFv2 namespace
        string attributeName = GetAttributeSimpleName(attribute);
        string baseName = attributeName.EndsWith("Attribute", StringComparison.Ordinal)
            ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
            : attributeName;

        // Build the new qualified name: System.Composition.{AttributeName}
        NameSyntax newName = SyntaxFactory.QualifiedName(
            SyntaxFactory.QualifiedName(
                SyntaxFactory.IdentifierName("System"),
                SyntaxFactory.IdentifierName("Composition")),
            SyntaxFactory.IdentifierName(baseName));

        // Handle attribute arguments - some may need adjustment
        AttributeArgumentListSyntax? newArguments = TransformArguments(attribute.ArgumentList, baseName);

        AttributeSyntax newAttribute = SyntaxFactory.Attribute(newName, newArguments)
            .WithLeadingTrivia(attribute.GetLeadingTrivia())
            .WithTrailingTrivia(attribute.GetTrailingTrivia());

        editor.ReplaceNode(attribute, newAttribute);

        return editor.GetChangedDocument();
    }

    private static AttributeArgumentListSyntax? TransformArguments(AttributeArgumentListSyntax? argumentList, string attributeName)
    {
        if (argumentList is null || argumentList.Arguments.Count == 0)
        {
            return null;
        }

        // For Import/ImportMany, MEFv2 doesn't support ContractType as a positional argument.
        // MEFv1: [Import(typeof(IService))] or [Import("name", typeof(IService))]
        // MEFv2: [Import] - contract type is inferred from member type.
        // We'll keep contract name arguments but drop explicit contract types.
        if (attributeName is "Import" or "ImportMany")
        {
            System.Collections.Generic.List<AttributeArgumentSyntax> newArgs = new();

            foreach (AttributeArgumentSyntax arg in argumentList.Arguments)
            {
                // Skip typeof() arguments (contract type) - MEFv2 infers this
                if (arg.Expression is TypeOfExpressionSyntax)
                {
                    continue;
                }

                // Keep string arguments (contract name) and named arguments
                newArgs.Add(arg);
            }

            if (newArgs.Count == 0)
            {
                return null;
            }

            return SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(newArgs));
        }

        // For Export, keep all arguments as-is (they're compatible)
        return argumentList;
    }
}
