// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Suppresses CS0649 for MEF importing members on exported parts,
/// since composition assigns them at runtime instead of in source.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CS0649ImportingMemberSuppressor : DiagnosticSuppressor
{
    /// <summary>
    /// The suppressor ID.
    /// </summary>
    public const string Id = "VSMEF018";

    private const string SuppressedDiagnosticId = "CS0649";

    /// <summary>
    /// The descriptor for this suppressor.
    /// </summary>
    internal static readonly SuppressionDescriptor Descriptor = new(
        id: Id,
        suppressedDiagnosticId: SuppressedDiagnosticId,
        justification: "Importing members on exported MEF parts are assigned at runtime by composition, so imported fields may appear unassigned in source.");

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

            ISymbol? affectedSymbol = ImportingMemberSuppressorUtilities.GetAffectedMemberSymbol(context, diagnostic);
            if (affectedSymbol is not IFieldSymbol and not IPropertySymbol)
            {
                continue;
            }

            INamedTypeSymbol? containingType = affectedSymbol.ContainingType;
            if (containingType is null ||
                !ImportingMemberSuppressorUtilities.HasInstanceExports(containingType) ||
                ImportingMemberSuppressorUtilities.HasPartNotDiscoverableAttribute(containingType))
            {
                continue;
            }

            if (ImportingMemberSuppressorUtilities.GetImportAttribute(affectedSymbol.GetAttributes()) is null)
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
        }
    }
}
