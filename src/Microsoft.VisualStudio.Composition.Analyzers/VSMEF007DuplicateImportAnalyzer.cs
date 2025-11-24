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

        var imports = new List<Import>();

        // Collect all imports from properties
        foreach (ISymbol member in namedType.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                AttributeData? importAttribute = Utils.GetImportAttribute(property.GetAttributes());

                if (importAttribute is not null)
                {
                    Contract contract = GetImportContract(importAttribute, property.Type);

                    imports.Add(new Import(contract, property.Name, property.Locations[0]));
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

                        Contract contract = GetImportContract(importAttribute, parameter.Type);

                        imports.Add(new Import(contract, parameter.Name, parameter.Locations[0]));
                    }
                }
            }
        }

        // Find duplicates
        IEnumerable<IGrouping<Contract, Import>> duplicateImportsByContract = imports.GroupBy(ic => ic.Contract).Where(g => g.Count() > 1);

        foreach ((Contract contract, IEnumerable<Import> duplicateImports) in duplicateImportsByContract)
        {
            foreach (Import import in duplicateImports)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptor,
                    import.Location,
                    namedType.Name,
                    contract));
            }
        }
    }

    private static Contract GetImportContract(AttributeData? importAttribute, ITypeSymbol importType)
    {
        string? explicitContractName = null;
        ITypeSymbol? explicitContractType = null;

        if (importAttribute is not null)
        {
            // Check for explicit contract name or type in constructor arguments.
            // Uses patterns from both System.Composition.ImportAttribute and System.ComponentModel.Composition.ImportAttribute.
            // An empty or null contract name means "no explicit name".
            (explicitContractName, explicitContractType) = importAttribute.ConstructorArguments switch
            {
                [TypedConstant { Value: string { Length: not 0 } contractName }] => (contractName, null),
                [TypedConstant { Value: INamedTypeSymbol contractType }] => (null, contractType),
                [TypedConstant { Value: string { Length: not 0 } contractName }, TypedConstant { Value: INamedTypeSymbol contractType }] => (contractName, contractType),
                [TypedConstant { Value: null or string { Length: 0 } }, TypedConstant { Value: INamedTypeSymbol contractType }] => (null, contractType),
                _ => (null, null),
            };

            // Check for contract name in named arguments
            TypedConstant? nameArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "ContractName").Value;
            if (nameArg?.Value is string { Length: not 0 } namedContractName)
            {
                explicitContractName = namedContractName;
            }

            // Check for contract type in named arguments
            TypedConstant? typeArg = importAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "ContractType").Value;
            if (typeArg?.Value is INamedTypeSymbol namedContractType)
            {
                explicitContractType = namedContractType;
            }
        }

        // Determine the base type for defaulting contract name and type.
        // If contract type is explicitly specified, use it; otherwise use the import parameter type.
        ITypeSymbol type = explicitContractType ?? importType;
        string typeName = type.ToDisplayString();

        // Contract name: use explicit name if provided, otherwise default to type name.
        string name = explicitContractName ?? typeName;

        return new Contract(typeName, name);
    }

    // A MEF contract consists of both a contract name and a contract type.
    // Both must match for two imports to be considered duplicates.
    // See: https://learn.microsoft.com/en-us/dotnet/framework/mef/attributed-programming-model-overview-mef#import-and-export-basics
    private readonly record struct Contract(string Type, string? Name)
    {
        public override string ToString() => this.Name is null ? this.Type : $"{this.Type} (\"{this.Name}\")";
    }

    private record Import(Contract Contract, string MemberName, Location Location);
}
