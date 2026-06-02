// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

/// <summary>
/// Suppresses CS8618 for MEF importing fields and properties on exported parts,
/// since MEF initializes such members after construction.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class CS8618ImportingMemberSuppressor : DiagnosticSuppressor
{
    /// <summary>
    /// The suppressor ID.
    /// </summary>
    public const string Id = "VSMEF014";

    private const string SuppressedDiagnosticId = "CS8618";

    /// <summary>
    /// The descriptor for this suppressor.
    /// </summary>
    internal static readonly SuppressionDescriptor Descriptor = new(
        id: Id,
        suppressedDiagnosticId: SuppressedDiagnosticId,
        justification: Strings.VSMEF014_Justification);

    /// <inheritdoc/>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(Descriptor);

    /// <inheritdoc/>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Id != SuppressedDiagnosticId)
            {
                continue;
            }

            ISymbol? affectedSymbol = this.GetAffectedMemberSymbol(context, diagnostic);
            if (affectedSymbol is not IFieldSymbol and not IPropertySymbol)
            {
                continue;
            }

            if (affectedSymbol is IPropertySymbol { SetMethod: null })
            {
                continue;
            }

            INamedTypeSymbol? containingType = affectedSymbol.ContainingType;
            if (containingType is null ||
                !Utils.HasInstanceExports(containingType) ||
                Utils.HasPartNotDiscoverableAttribute(containingType))
            {
                continue;
            }

            AttributeData? importAttribute = Utils.GetImportAttribute(affectedSymbol.GetAttributes());
            if (importAttribute is null || Utils.GetAllowDefaultValue(importAttribute))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
        }
    }

    private ISymbol? GetAffectedMemberSymbol(SuppressionAnalysisContext context, Diagnostic diagnostic)
    {
        foreach (Location location in diagnostic.AdditionalLocations)
        {
            ISymbol? symbol = GetDeclaredSymbol(context, location);
            if (symbol is IFieldSymbol or IPropertySymbol)
            {
                return symbol;
            }
        }

        return GetDeclaredSymbol(context, diagnostic.Location);
    }

    private static ISymbol? GetDeclaredSymbol(SuppressionAnalysisContext context, Location location)
    {
        SyntaxTree? syntaxTree = location.SourceTree;
        if (syntaxTree is null)
        {
            return null;
        }

        SemanticModel semanticModel = context.GetSemanticModel(syntaxTree);
        SyntaxNode root = syntaxTree.GetRoot(context.CancellationToken);
        SyntaxNode? node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

        while (node is not null)
        {
            ISymbol? symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
            if (symbol is not null)
            {
                return symbol;
            }

            node = node.Parent;
        }

        return null;
    }
}
