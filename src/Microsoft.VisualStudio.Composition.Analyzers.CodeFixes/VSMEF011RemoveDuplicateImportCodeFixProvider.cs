// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

/// <summary>
/// Provides code fixes to remove duplicate Import/ImportMany attributes.
/// </summary>
/// <remarks>
/// The code fix automatically chooses the correct attribute to remove based on the member type:
/// <list type="bullet">
/// <item><description>For collection types (IEnumerable, ICollection, arrays, etc.), it removes [Import] and keeps [ImportMany].</description></item>
/// <item><description>For non-collection types, it removes [ImportMany] and keeps [Import].</description></item>
/// </list>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VSMEF011RemoveDuplicateImportCodeFixProvider))]
[Shared]
public class VSMEF011RemoveDuplicateImportCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(VSMEF011BothImportAndImportManyAnalyzer.Id);

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

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

            // Find the Import and ImportMany attributes on this symbol
            ISymbol? symbol = GetSymbolFromNode(node, semanticModel, context.CancellationToken);
            if (symbol is null)
            {
                continue;
            }

            (AttributeData? importAttr, AttributeData? importManyAttr) = FindImportAttributes(symbol);
            if (importAttr is null || importManyAttr is null)
            {
                continue;
            }

            // Determine if the member type is a collection
            ITypeSymbol? memberType = GetMemberType(symbol);
            bool isCollectionType = memberType is not null && IsCollectionType(memberType);

            if (isCollectionType)
            {
                // For collections, keep [ImportMany] (remove [Import])
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Strings.VSMEF011_CodeFix_RemoveImportKeepImportMany,
                        createChangedDocument: ct => RemoveAttributeAsync(context.Document, importAttr, ct),
                        equivalenceKey: "RemoveImport"),
                    diagnostic);
            }
            else
            {
                // For non-collections, keep [Import] (remove [ImportMany])
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Strings.VSMEF011_CodeFix_RemoveImportManyKeepImport,
                        createChangedDocument: ct => RemoveAttributeAsync(context.Document, importManyAttr, ct),
                        equivalenceKey: "RemoveImportMany"),
                    diagnostic);
            }
        }
    }

    private static ITypeSymbol? GetMemberType(ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol prop => prop.Type,
            IFieldSymbol field => field.Type,
            IParameterSymbol param => param.Type,
            _ => null,
        };
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Check for arrays
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        // Check for common collection interfaces
        if (type is INamedTypeSymbol namedType)
        {
            string fullName = namedType.ConstructedFrom.ToDisplayString();
            if (fullName.StartsWith("System.Collections.Generic.IEnumerable<", System.StringComparison.Ordinal) ||
                fullName.StartsWith("System.Collections.Generic.ICollection<", System.StringComparison.Ordinal) ||
                fullName.StartsWith("System.Collections.Generic.IList<", System.StringComparison.Ordinal) ||
                fullName.StartsWith("System.Collections.Generic.IReadOnlyCollection<", System.StringComparison.Ordinal) ||
                fullName.StartsWith("System.Collections.Generic.IReadOnlyList<", System.StringComparison.Ordinal) ||
                fullName.StartsWith("System.Collections.Generic.List<", System.StringComparison.Ordinal) ||
                fullName.StartsWith("System.Collections.Generic.HashSet<", System.StringComparison.Ordinal) ||
                fullName == "System.Collections.IEnumerable" ||
                fullName == "System.Collections.ICollection" ||
                fullName == "System.Collections.IList")
            {
                return true;
            }

            // Check if it implements IEnumerable<T> (but not string which is IEnumerable<char>)
            if (fullName != "string" && fullName != "System.String")
            {
                foreach (INamedTypeSymbol iface in namedType.AllInterfaces)
                {
                    string ifaceName = iface.ConstructedFrom.ToDisplayString();
                    if (ifaceName.StartsWith("System.Collections.Generic.IEnumerable<", System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static ISymbol? GetSymbolFromNode(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // The node might be the member name or the whole declaration
        return node switch
        {
            PropertyDeclarationSyntax prop => semanticModel.GetDeclaredSymbol(prop, cancellationToken),
            FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault() is { } variable
                ? semanticModel.GetDeclaredSymbol(variable, cancellationToken)
                : null,
            VariableDeclaratorSyntax variable => semanticModel.GetDeclaredSymbol(variable, cancellationToken),
            ParameterSyntax param => semanticModel.GetDeclaredSymbol(param, cancellationToken),
            _ => node.Parent is not null ? GetSymbolFromNode(node.Parent, semanticModel, cancellationToken) : null,
        };
    }

    private static (AttributeData? Import, AttributeData? ImportMany) FindImportAttributes(ISymbol symbol)
    {
        AttributeData? importAttr = null;
        AttributeData? importManyAttr = null;

        foreach (AttributeData attr in symbol.GetAttributes())
        {
            string? fullName = attr.AttributeClass?.ToDisplayString();
            if (fullName is "System.ComponentModel.Composition.ImportAttribute" or "System.Composition.ImportAttribute")
            {
                importAttr = attr;
            }
            else if (fullName is "System.ComponentModel.Composition.ImportManyAttribute" or "System.Composition.ImportManyAttribute")
            {
                importManyAttr = attr;
            }
        }

        return (importAttr, importManyAttr);
    }

    private static async Task<Document> RemoveAttributeAsync(
        Document document,
        AttributeData attribute,
        CancellationToken cancellationToken)
    {
        if (attribute.ApplicationSyntaxReference is null)
        {
            return document;
        }

        SyntaxNode attributeSyntax = await attribute.ApplicationSyntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
        if (attributeSyntax is not AttributeSyntax attrSyntax)
        {
            return document;
        }

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Check if this is the only attribute in the attribute list
        if (attrSyntax.Parent is AttributeListSyntax attrList && attrList.Attributes.Count == 1)
        {
            // Remove the entire attribute list (including brackets)
            editor.RemoveNode(attrList);
        }
        else
        {
            // Remove just this attribute from the list
            editor.RemoveNode(attrSyntax);
        }

        return editor.GetChangedDocument();
    }
}
