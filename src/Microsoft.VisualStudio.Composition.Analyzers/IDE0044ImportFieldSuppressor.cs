// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

/// <summary>
/// Suppresses IDE0044 ("Make field readonly") for fields decorated with MEF
/// <c>[Import]</c>, <c>[ImportMany]</c>, <c>[System.Composition.Import]</c>, or
/// <c>[System.Composition.ImportMany]</c> attributes,
/// since such fields are assigned at runtime via reflection and cannot be made readonly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class IDE0044ImportFieldSuppressor : DiagnosticSuppressor
{
    /// <summary>
    /// The suppressor ID.
    /// </summary>
    public const string Id = "VSMEF013";

    private const string SuppressedDiagnosticId = "IDE0044";

    /// <summary>
    /// The descriptor for this suppressor.
    /// </summary>
    internal static readonly SuppressionDescriptor Descriptor = new(
        id: Id,
        suppressedDiagnosticId: SuppressedDiagnosticId,
        justification: Strings.VSMEF013_Justification);

    /// <inheritdoc/>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(Descriptor);

    /// <inheritdoc/>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            SyntaxTree? syntaxTree = diagnostic.Location.SourceTree;
            if (syntaxTree is null)
            {
                continue;
            }

            SemanticModel semanticModel = context.GetSemanticModel(syntaxTree);
            SyntaxNode root = syntaxTree.GetRoot(context.CancellationToken);
            SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

            IFieldSymbol? field = null;
            while (node is not null)
            {
                ISymbol? symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
                if (symbol is IFieldSymbol f)
                {
                    field = f;
                    break;
                }

                node = node.Parent;
            }

            if (field is null)
            {
                continue;
            }

            foreach (AttributeData attribute in field.GetAttributes())
            {
                if (Utils.IsImportAttribute(attribute.AttributeClass))
                {
                    context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
                    break;
                }
            }
        }
    }
}
