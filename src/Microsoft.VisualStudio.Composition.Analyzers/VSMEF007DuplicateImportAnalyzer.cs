// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when a type imports the same contract more than once.
/// </summary>
/// <remarks>
/// This analyzer detects when a type has multiple imports (properties, constructor parameters, or a mix)
/// that import the same contract. For imports with specific contract names, it only flags duplicates
/// when the contract names match exactly.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF007DuplicateImportAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF007";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF007_Title,
        messageFormat: Strings.VSMEF007_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            // Only scan further if the compilation references the assemblies that define the attributes we'll be looking for.
            if (Utils.ReferencesMefAttributes(context.Compilation))
            {
                context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Skip interfaces, enums, and other non-class types
        if (namedType.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        var importContracts = new List<ImportContract>();

        // Collect all imports from properties
        foreach (ISymbol member in namedType.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                AttributeData? importAttribute = Utils.GetImportAttribute(property.GetAttributes());

                string? contract = GetImportContract(importAttribute, property.Type, allowImplicitImport: false);

                if (contract is not null)
                {
                    importContracts.Add(new ImportContract(contract, property.Name, property.Locations[0]));
                }
            }
        }

        // Collect all imports from constructor parameters
        foreach (ISymbol member in namedType.GetMembers())
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                bool isImportingConstructor = Utils.HasImportingConstructorAttribute(method);

                if (isImportingConstructor)
                {
                    foreach (IParameterSymbol parameter in method.Parameters)
                    {
                        AttributeData? importAttribute = Utils.GetImportAttribute(parameter.GetAttributes());

                        string? contract = GetImportContract(importAttribute, parameter.Type, allowImplicitImport: true);

                        if (contract is not null)
                        {
                            importContracts.Add(new ImportContract(contract, parameter.Name, parameter.Locations[0]));
                        }
                    }
                }
            }
        }

        // Find duplicates
        IEnumerable<IGrouping<string, ImportContract>> contractGroups = importContracts.GroupBy(ic => ic.Contract).Where(g => g.Count() > 1);

        foreach (IGrouping<string, ImportContract>? group in contractGroups)
        {
            foreach (ImportContract? import in group)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptor,
                    import.Location,
                    namedType.Name,
                    import.Contract));
            }
        }
    }

    private static string? GetImportContract(AttributeData? importAttribute, ITypeSymbol importType, bool allowImplicitImport)
    {
        // Note that the actual contract name used by MEF is more complex than this. See ContractNameServices
        // for the full logic. This approximation suffices for catching duplicates, for the purposes of this analyzer.
        if (importAttribute is not null)
        {
            // Check for explicit contract name or type in constructor arguments
            if (importAttribute.ConstructorArguments is [TypedConstant firstArg, ..])
            {
                if (firstArg.Value is string { Length: not 0 } contractName)
                {
                    return contractName;
                }

                if (firstArg.Value is INamedTypeSymbol contractType)
                {
                    return contractType.ToDisplayString();
                }
            }

            // Check for contract name in named arguments
            KeyValuePair<string, TypedConstant> contractNameArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "ContractName");
            if (contractNameArg.Key is not null && contractNameArg.Value.Value is string namedContractName && !string.IsNullOrEmpty(namedContractName))
            {
                return namedContractName;
            }

            // Check for contract type in named arguments
            KeyValuePair<string, TypedConstant> contractTypeArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "ContractType");
            if (contractTypeArg.Key is not null && contractTypeArg.Value.Value is INamedTypeSymbol namedContractType)
            {
                return namedContractType.ToDisplayString();
            }

            // Default contract is the type name
            return importType.ToDisplayString();
        }

        // If no import attribute and implicit imports are allowed (e.g., in ImportingConstructor),
        // treat the parameter as an implicit import with the type as the contract
        if (allowImplicitImport)
        {
            return importType.ToDisplayString();
        }

        // No import attribute and implicit imports not allowed means this is not an import
        return null;
    }

    private record ImportContract(string Contract, string MemberName, Location Location);
}
