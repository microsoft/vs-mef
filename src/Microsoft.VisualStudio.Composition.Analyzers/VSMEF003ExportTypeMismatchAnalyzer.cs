// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

/// <summary>
/// Creates a diagnostic when `[Export(typeof(T))]` is applied to a class that does not implement T,
/// or to a property whose type is not compatible with T.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF003ExportTypeMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF003";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF003_Title,
        messageFormat: Strings.VSMEF003_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            // Only scan further if the compilation references the assemblies that define the attributes we'll be looking for.
            if (context.Compilation.ReferencedAssemblyNames.Any(i =>
                string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Name, "System.Composition.AttributedModel", StringComparison.OrdinalIgnoreCase)))
            {
                INamedTypeSymbol? mefV1ExportAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ExportAttribute");
                INamedTypeSymbol? mefV2ExportAttribute = context.Compilation.GetTypeByMetadataName("System.Composition.ExportAttribute");
                context.RegisterSymbolAction(
                    context => AnalyzeTypeDeclaration(context, mefV1ExportAttribute, mefV2ExportAttribute),
                    SymbolKind.NamedType);
                context.RegisterSymbolAction(
                    context => AnalyzePropertyDeclaration(context, mefV1ExportAttribute, mefV2ExportAttribute),
                    SymbolKind.Property);
            }
        });
    }

    private static void AnalyzeTypeDeclaration(SymbolAnalysisContext context, INamedTypeSymbol? mefV1ExportAttribute, INamedTypeSymbol? mefV2ExportAttribute)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Skip interfaces, enums, delegates - only analyze classes
        if (namedType.TypeKind != TypeKind.Class)
        {
            return;
        }

        Location? location = namedType.Locations.FirstOrDefault();
        if (location is null)
        {
            // We won't have anywhere to publish a diagnostic anyway.
            return;
        }

        foreach (AttributeData attributeData in namedType.GetAttributes())
        {
            // Check if this is an Export attribute
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, mefV1ExportAttribute) ||
                SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, mefV2ExportAttribute))
            {
                // Check if the export attribute has a type argument
                if (attributeData.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol exportedType }, ..])
                {
                    // Check if the exporting type implements or inherits from the exported type
                    if (!IsTypeCompatible(namedType, exportedType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Descriptor,
                            location,
                            namedType.Name,
                            exportedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                    }
                }
            }
        }
    }

    private static void AnalyzePropertyDeclaration(SymbolAnalysisContext context, INamedTypeSymbol? mefV1ExportAttribute, INamedTypeSymbol? mefV2ExportAttribute)
    {
        var property = (IPropertySymbol)context.Symbol;

        Location? location = property.Locations.FirstOrDefault();
        if (location is null)
        {
            // We won't have anywhere to publish a diagnostic anyway.
            return;
        }

        foreach (AttributeData attributeData in property.GetAttributes())
        {
            // Check if this is an Export attribute
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, mefV1ExportAttribute) ||
                SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, mefV2ExportAttribute))
            {
                // Check if the export attribute has a type argument
                if (attributeData.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol exportedType }, ..])
                {
                    // Check if the property type is compatible with the exported type
                    if (!IsPropertyTypeCompatible(property.Type, exportedType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Descriptor,
                            location,
                            property.Name,
                            exportedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                    }
                }
            }
        }
    }

    private static bool IsTypeCompatible(INamedTypeSymbol implementingType, INamedTypeSymbol exportedType)
    {
        // If they're the same type, it's compatible
        if (SymbolEqualityComparer.Default.Equals(implementingType, exportedType))
        {
            return true;
        }

        // Check if implementing type inherits from exported type (for classes)
        if (exportedType.TypeKind == TypeKind.Class)
        {
            INamedTypeSymbol? currentType = implementingType.BaseType;
            while (currentType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(currentType, exportedType))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }
        }

        // Check if implementing type implements exported interface
        if (exportedType.TypeKind == TypeKind.Interface)
        {
            return implementingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(exportedType.IsUnboundGenericType ? i.ConstructUnboundGenericType() : i, exportedType));
        }

        return false;
    }

    private static bool IsPropertyTypeCompatible(ITypeSymbol propertyType, INamedTypeSymbol exportedType)
    {
        // If they're the same type, it's compatible
        if (SymbolEqualityComparer.Default.Equals(propertyType, exportedType))
        {
            return true;
        }

        // If property type is a named type, use the same logic as for classes
        if (propertyType is INamedTypeSymbol namedPropertyType)
        {
            return IsTypeCompatible(namedPropertyType, exportedType);
        }

        return false;
    }
}
