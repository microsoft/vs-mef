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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Provides code fixes for VSMEF004: Exported type missing importing constructor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public class VSMEF004ExportWithoutImportingConstructorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        VSMEF004ExportWithoutImportingConstructorAnalyzer.Id);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        Diagnostic? diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == VSMEF004ExportWithoutImportingConstructorAnalyzer.Id);
        if (diagnostic is null)
        {
            return;
        }

        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        SyntaxNode constructorNode = root.FindNode(diagnosticSpan);

        // Find the constructor declaration
        ConstructorDeclarationSyntax? constructorDeclaration = constructorNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (constructorDeclaration is null)
        {
            return;
        }

        ClassDeclarationSyntax? classDeclaration = constructorDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration is null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        // Determine which MEF version to use based on existing attributes
        MefVersion mefVersion = DetermineMefVersion(classDeclaration, semanticModel);

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
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Create the ImportingConstructor attribute with proper annotations for simplification
        string attributeName = mefVersion == MefVersion.V1
            ? "System.ComponentModel.Composition.ImportingConstructor"
            : "System.Composition.ImportingConstructor";

        AttributeSyntax attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName(attributeName)
                .WithAdditionalAnnotations(Simplifier.AddImportsAnnotation, Simplifier.Annotation));
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));

        // Add the attribute to the constructor
        ConstructorDeclarationSyntax newConstructor = constructorDeclaration.WithAttributeLists(
            constructorDeclaration.AttributeLists.Add(attributeList));

        SyntaxNode newRoot = root.ReplaceNode(constructorDeclaration, newConstructor);

        // Apply simplification and formatting
        document = document.WithSyntaxRoot(newRoot);
        document = await ImportAdder.AddImportsAsync(document, Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken).ConfigureAwait(false);
        document = await Simplifier.ReduceAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);

        return document;
    }

    private static async Task<Document> AddParameterlessConstructorAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Create a parameterless constructor
        ConstructorDeclarationSyntax newConstructor = SyntaxFactory.ConstructorDeclaration(
                SyntaxFactory.Identifier(classDeclaration.Identifier.ValueText))
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList())
            .WithBody(SyntaxFactory.Block())
            .WithAdditionalAnnotations(Formatter.Annotation);

        // Find the position to insert the constructor (before existing constructors)
        var existingConstructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().ToList();
        int insertIndex = 0;

        if (existingConstructors.Any())
        {
            // Insert before the first constructor
            ConstructorDeclarationSyntax firstConstructor = existingConstructors.First();
            insertIndex = classDeclaration.Members.IndexOf(firstConstructor);
        }
        else
        {
            // Insert at the beginning of the class
            insertIndex = 0;
        }

        SyntaxList<MemberDeclarationSyntax> newMembers = classDeclaration.Members.Insert(insertIndex, newConstructor);
        ClassDeclarationSyntax newClass = classDeclaration.WithMembers(newMembers);

        SyntaxNode newRoot = root.ReplaceNode(classDeclaration, newClass);

        // Apply formatting
        Document newDocument = document.WithSyntaxRoot(newRoot);
        newDocument = await Formatter.FormatAsync(newDocument, Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);

        return newDocument;
    }

    private static MefVersion DetermineMefVersion(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        // Check the class and its members for MEF attributes to determine which version to use
        INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol is null)
        {
            return MefVersion.V2; // Default to V2
        }

        // Check class-level attributes
        foreach (AttributeData attribute in classSymbol.GetAttributes())
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
        foreach (ISymbol member in classSymbol.GetMembers())
        {
            foreach (AttributeData attribute in member.GetAttributes())
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

        string? namespaceName = attributeType.ContainingNamespace?.ToDisplayString();
        return namespaceName == "System.ComponentModel.Composition" ||
                namespaceName?.StartsWith("System.ComponentModel.Composition.") == true;
    }

    private static bool IsMefV2Attribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        string? namespaceName = attributeType.ContainingNamespace?.ToDisplayString();
        return namespaceName == "System.Composition" ||
                namespaceName?.StartsWith("System.Composition.") == true;
    }

    private enum MefVersion
    {
        V1,
        V2,
    }
}
