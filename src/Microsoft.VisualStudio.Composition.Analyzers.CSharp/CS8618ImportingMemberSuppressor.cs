// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Suppresses CS8618 for MEF importing fields and properties on exported parts,
/// since MEF initializes such members after construction.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
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
        justification: "Importing members on exported MEF parts are initialized after construction unless AllowDefault = true is specified.");

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

            if (affectedSymbol is IPropertySymbol { SetMethod: null })
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

            AttributeData? importAttribute = ImportingMemberSuppressorUtilities.GetImportAttribute(affectedSymbol.GetAttributes());
            if (importAttribute is null || ImportingMemberSuppressorUtilities.GetAllowDefaultValue(importAttribute))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
        }
    }
}
