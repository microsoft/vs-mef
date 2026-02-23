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
/// <para>
/// This analyzer detects when a type has multiple imports (properties, constructor parameters, or a mix)
/// that import the same contract. For imports with specific contract names, it only flags duplicates
/// when the contract names match exactly.
/// </para>
/// <para>
/// Note: This analyzer can produce false positives. If a MEF part is exported as non-shared, then it
/// may make sense to have multiple seemingly-identical imports and use them independently. This analyzer
/// considers the import-site RequiredCreationPolicy attribute, but cannot detect cases where the exported
/// type itself is marked as NonShared, since that information is not statically available at the import site.
/// In such cases, runtime composition may succeed even though this analyzer reports a warning, and the developer
/// should suppress the instance of the diagnostic. This should be very rare however.
/// </para>
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
                INamedTypeSymbol? lazyType = context.Compilation.GetTypeByMetadataName("System.Lazy`1");
                INamedTypeSymbol? lazyWithMetadataType = context.Compilation.GetTypeByMetadataName("System.Lazy`2");
                INamedTypeSymbol? exportFactoryV1Type = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ExportFactory`1");
                INamedTypeSymbol? exportFactoryV1WithMetadataType = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ExportFactory`2");
                INamedTypeSymbol? exportFactoryV2Type = context.Compilation.GetTypeByMetadataName("System.Composition.ExportFactory`1");
                INamedTypeSymbol? exportFactoryV2WithMetadataType = context.Compilation.GetTypeByMetadataName("System.Composition.ExportFactory`2");

                var wrapperTypes = new WrapperTypes(
                    lazyType,
                    lazyWithMetadataType,
                    exportFactoryV1Type,
                    exportFactoryV1WithMetadataType,
                    exportFactoryV2Type,
                    exportFactoryV2WithMetadataType);

                context.RegisterSymbolAction(ctx => AnalyzeType(ctx, wrapperTypes), SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, WrapperTypes wrapperTypes)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Skip interfaces, enums, and other non-class types
        if (namedType.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        var imports = new List<Import>();

        // Collect all imports from properties
        // Note: We skip NonShared imports because each gets a unique instance, so they're not problematic duplicates.
        // However, we don't skip ExportFactory imports even with NonShared, because ExportFactory already creates
        // new instances on demand - having multiple identical ExportFactory imports is still redundant.
        foreach (ISymbol member in namedType.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                AttributeData? importAttribute = Utils.GetImportAttribute(property.GetAttributes());

                if (importAttribute is not null)
                {
                    if (Utils.IsNonSharedImport(importAttribute) && !IsExportFactoryType(property.Type, wrapperTypes))
                    {
                        // Skip NonShared imports (except ExportFactory), each gets a unique instance.
                        continue;
                    }

                    Contract contract = GetImportContract(importAttribute, property.Type, wrapperTypes);

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

                        if (importAttribute is not null && Utils.IsNonSharedImport(importAttribute) && !IsExportFactoryType(parameter.Type, wrapperTypes))
                        {
                            // Skip NonShared imports (except ExportFactory), each gets a unique instance.
                            continue;
                        }

                        Contract contract = GetImportContract(importAttribute, parameter.Type, wrapperTypes);

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

    private static Contract GetImportContract(AttributeData? importAttribute, ITypeSymbol importType, WrapperTypes wrapperTypes)
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
        // Note that the actual contract name used by MEF is more complex than this. See ContractNameServices
        // for the full logic. This approximation suffices for catching duplicates, for the purposes of this analyzer.

        // If contract type is explicitly specified, use it; otherwise use the import parameter type.
        ITypeSymbol type = explicitContractType ?? importType;

        // Unwrap Lazy<T>, Lazy<T, TMetadata>, ExportFactory<T>, ExportFactory<T, TMetadata>
        // This ensures that importing T and Lazy<T> are correctly detected as duplicates.
        type = UnwrapLazyOrExportFactory(type, wrapperTypes);

        string typeName = type.ToDisplayString();

        // Contract name: use explicit name if provided, otherwise default to type name.
        string name = explicitContractName ?? typeName;

        return new Contract(typeName, name);
    }

    private static ITypeSymbol UnwrapLazyOrExportFactory(ITypeSymbol type, WrapperTypes wrapperTypes)
    {
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return type;
        }

        INamedTypeSymbol originalDef = namedType.OriginalDefinition;

        // Check for Lazy<T> or Lazy<T, TMetadata>
        if (SymbolEqualityComparer.Default.Equals(originalDef, wrapperTypes.LazyType) ||
            SymbolEqualityComparer.Default.Equals(originalDef, wrapperTypes.LazyWithMetadataType))
        {
            return namedType.TypeArguments[0];
        }

        // Check for ExportFactory<T> or ExportFactory<T, TMetadata> (both V1 and V2)
        if (IsExportFactoryType(type, wrapperTypes))
        {
            return namedType.TypeArguments[0];
        }

        return type;
    }

    private static bool IsExportFactoryType(ITypeSymbol type, WrapperTypes wrapperTypes)
    {
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return false;
        }

        INamedTypeSymbol originalDef = namedType.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(originalDef, wrapperTypes.ExportFactoryV1Type) ||
               SymbolEqualityComparer.Default.Equals(originalDef, wrapperTypes.ExportFactoryV1WithMetadataType) ||
               SymbolEqualityComparer.Default.Equals(originalDef, wrapperTypes.ExportFactoryV2Type) ||
               SymbolEqualityComparer.Default.Equals(originalDef, wrapperTypes.ExportFactoryV2WithMetadataType);
    }

    private sealed record WrapperTypes(
        INamedTypeSymbol? LazyType,
        INamedTypeSymbol? LazyWithMetadataType,
        INamedTypeSymbol? ExportFactoryV1Type,
        INamedTypeSymbol? ExportFactoryV1WithMetadataType,
        INamedTypeSymbol? ExportFactoryV2Type,
        INamedTypeSymbol? ExportFactoryV2WithMetadataType);

    // A MEF contract consists of both a contract name and a contract type.
    // Both must match for two imports to be considered duplicates.
    // See: https://learn.microsoft.com/en-us/dotnet/framework/mef/attributed-programming-model-overview-mef#import-and-export-basics
    private readonly record struct Contract(string Type, string? Name)
    {
        public override string ToString() => this.Name is null || string.Equals(this.Name, this.Type) ? this.Type : $"{this.Type} (\"{this.Name}\")";
    }

    private record Import(Contract Contract, string MemberName, Location Location);
}
