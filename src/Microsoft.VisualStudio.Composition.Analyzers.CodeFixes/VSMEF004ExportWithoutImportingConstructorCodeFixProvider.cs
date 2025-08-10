// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.CodeFixes;

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
/// Provides code fixes for VSMEF004: Exported type missing importing constructor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VSMEF004ExportWithoutImportingConstructorCodeFixProvider))]
[Shared]
public class VSMEF004ExportWithoutImportingConstructorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(VSMEF004ExportWithoutImportingConstructorAnalyzer.Id);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == VSMEF004ExportWithoutImportingConstructorAnalyzer.Id);
        if (diagnostic is null)
        {
            return;
        }

        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var constructorNode = root.FindNode(diagnosticSpan);

        // Find the constructor declaration
        var constructorDeclaration = constructorNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (constructorDeclaration is null)
        {
            return;
        }

        var classDeclaration = constructorDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        // Determine which MEF version to use based on existing attributes
        var mefVersion = DetermineMefVersion(classDeclaration, semanticModel);

        // Primary fix: Add [ImportingConstructor] attribute
        var addAttributeAction = CodeAction.Create(
            title: "Add [ImportingConstructor] attribute",
            createChangedDocument: c => AddImportingConstructorAttributeAsync(context.Document, constructorDeclaration, mefVersion, c),
            equivalenceKey: "AddImportingConstructorAttribute");

        context.RegisterCodeFix(addAttributeAction, diagnostic);

        // Secondary fix: Add parameterless constructor
        var addConstructorAction = CodeAction.Create(
            title: "Add parameterless constructor",
            createChangedDocument: c => AddParameterlessConstructorAsync(context.Document, classDeclaration, c),
            equivalenceKey: "AddParameterlessConstructor");

        context.RegisterCodeFix(addConstructorAction, diagnostic);
    }

    private static async Task<Document> AddImportingConstructorAttributeAsync(
        Document document,
        ConstructorDeclarationSyntax constructorDeclaration,
        MefVersion mefVersion,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Create the ImportingConstructor attribute
        var attributeName = mefVersion == MefVersion.V1
            ? "System.ComponentModel.Composition.ImportingConstructor"
            : "System.Composition.ImportingConstructor";

        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(attributeName));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));

        // Add the attribute to the constructor
        var newConstructor = constructorDeclaration.WithAttributeLists(
            constructorDeclaration.AttributeLists.Add(attributeList));

        var newRoot = root.ReplaceNode(constructorDeclaration, newConstructor);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddParameterlessConstructorAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Create a parameterless constructor
        var newConstructor = SyntaxFactory.ConstructorDeclaration(classDeclaration.Identifier)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList())
            .WithBody(SyntaxFactory.Block());

        // Find the position to insert the constructor (after existing constructors)
        var existingConstructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().ToList();
        int insertIndex = 0;

        if (existingConstructors.Any())
        {
            // Insert after the last constructor
            var lastConstructor = existingConstructors.Last();
            insertIndex = classDeclaration.Members.IndexOf(lastConstructor) + 1;
        }
        else
        {
            // Insert at the beginning of the class
            insertIndex = 0;
        }

        var newMembers = classDeclaration.Members.Insert(insertIndex, newConstructor);
        var newClass = classDeclaration.WithMembers(newMembers);

        var newRoot = root.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }

    private static MefVersion DetermineMefVersion(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        // Check the class and its members for MEF attributes to determine which version to use
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol is null)
        {
            return MefVersion.V2; // Default to V2
        }

        // Check class-level attributes
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (IsMefV1Attribute(attribute.AttributeClass))
            {
                return MefVersion.V1;
            }

            if (IsMefV2Attribute(attribute.AttributeClass))
            {
                return MefVersion.V2;
            }
        }

        // Check member-level attributes
        foreach (var member in classSymbol.GetMembers())
        {
            foreach (var attribute in member.GetAttributes())
            {
                if (IsMefV1Attribute(attribute.AttributeClass))
                {
                    return MefVersion.V1;
                }

                if (IsMefV2Attribute(attribute.AttributeClass))
                {
                    return MefVersion.V2;
                }
            }
        }

        return MefVersion.V2; // Default to V2 if no MEF attributes found
    }

    private static bool IsMefV1Attribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        var namespaceName = attributeType.ContainingNamespace?.ToDisplayString();
        return namespaceName == "System.ComponentModel.Composition" ||
                namespaceName?.StartsWith("System.ComponentModel.Composition.") == true;
    }

    private static bool IsMefV2Attribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        var namespaceName = attributeType.ContainingNamespace?.ToDisplayString();
        return namespaceName == "System.Composition" ||
                namespaceName?.StartsWith("System.Composition.") == true;
    }

    private enum MefVersion
    {
        V1,
        V2,
    }
}
