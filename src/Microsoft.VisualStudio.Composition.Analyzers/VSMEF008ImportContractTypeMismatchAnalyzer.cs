// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when an Import attribute specifies a contract type that is not assignable to the member type.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer detects when [Import(typeof(T))] or [ImportMany(typeof(T))] specifies a contract type
/// that is incompatible with the property, field, or parameter type. The analyzer unwraps Lazy&lt;T&gt;,
/// ExportFactory&lt;T&gt;, and collection types to determine the actual expected type.
/// </para>
/// <para>
/// Note: This analyzer only applies to MEFv1 attributes because MEFv2 does not support explicit contract types.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF008ImportContractTypeMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF008";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF008_Title,
        messageFormat: Strings.VSMEF008_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
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
            // Only scan further if the compilation references MEFv1 attributes (MEFv2 doesn't support contract types)
            if (!context.Compilation.ReferencedAssemblyNames.Any(i =>
                string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            INamedTypeSymbol? mefV1ImportAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportAttribute");
            INamedTypeSymbol? mefV1ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportManyAttribute");
            INamedTypeSymbol? lazyType = context.Compilation.GetTypeByMetadataName("System.Lazy`1");
            INamedTypeSymbol? lazyWithMetadataType = context.Compilation.GetTypeByMetadataName("System.Lazy`2");
            INamedTypeSymbol? exportFactoryType = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ExportFactory`1");
            INamedTypeSymbol? exportFactoryWithMetadataType = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ExportFactory`2");
            INamedTypeSymbol? ienumerableType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");

            if (mefV1ImportAttribute is null && mefV1ImportManyAttribute is null)
            {
                return;
            }

            var state = new AnalyzerState(
                mefV1ImportAttribute,
                mefV1ImportManyAttribute,
                lazyType,
                lazyWithMetadataType,
                exportFactoryType,
                exportFactoryWithMetadataType,
                ienumerableType);

            context.RegisterSymbolAction(ctx => AnalyzeProperty(ctx, state), SymbolKind.Property);
            context.RegisterSymbolAction(ctx => AnalyzeField(ctx, state), SymbolKind.Field);
            context.RegisterSymbolAction(ctx => AnalyzeMethod(ctx, state), SymbolKind.Method);
        });
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, AnalyzerState state)
    {
        var property = (IPropertySymbol)context.Symbol;
        AnalyzeMember(context, state, property.Type, property.GetAttributes(), property.Locations.FirstOrDefault());
    }

    private static void AnalyzeField(SymbolAnalysisContext context, AnalyzerState state)
    {
        var field = (IFieldSymbol)context.Symbol;
        AnalyzeMember(context, state, field.Type, field.GetAttributes(), field.Locations.FirstOrDefault());
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, AnalyzerState state)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Only analyze constructors with ImportingConstructor attribute
        if (method.MethodKind != MethodKind.Constructor || !Utils.HasImportingConstructorAttribute(method))
        {
            return;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            AnalyzeMember(context, state, parameter.Type, parameter.GetAttributes(), parameter.Locations.FirstOrDefault());
        }
    }

    private static void AnalyzeMember(
        SymbolAnalysisContext context,
        AnalyzerState state,
        ITypeSymbol memberType,
        ImmutableArray<AttributeData> attributes,
        Location? location)
    {
        if (location is null)
        {
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            INamedTypeSymbol? attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            bool isImport = SymbolEqualityComparer.Default.Equals(attributeClass, state.MefV1ImportAttribute);
            bool isImportMany = SymbolEqualityComparer.Default.Equals(attributeClass, state.MefV1ImportManyAttribute);

            if (!isImport && !isImportMany)
            {
                continue;
            }

            // Get the explicit contract type from the attribute
            INamedTypeSymbol? explicitContractType = GetExplicitContractType(attribute);
            if (explicitContractType is null)
            {
                // No explicit contract type specified, nothing to check
                continue;
            }

            // Get the expected type from the member, unwrapping wrappers and collections
            ITypeSymbol expectedType = GetExpectedType(memberType, isImportMany, state);

            // Check if the contract type is assignable to the expected type
            if (!IsAssignableTo(explicitContractType, expectedType, context.Compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptor,
                    location,
                    explicitContractType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    expectedType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            }
        }
    }

    private static INamedTypeSymbol? GetExplicitContractType(AttributeData attribute)
    {
        // Check constructor arguments for contract type
        // Import(Type contractType) or Import(string contractName, Type contractType)
        foreach (TypedConstant arg in attribute.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol contractType)
            {
                return contractType;
            }
        }

        // Check named arguments for ContractType
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "ContractType" && namedArg.Value.Value is INamedTypeSymbol contractType)
            {
                return contractType;
            }
        }

        return null;
    }

    private static ITypeSymbol GetExpectedType(ITypeSymbol memberType, bool isImportMany, AnalyzerState state)
    {
        ITypeSymbol type = memberType;

        // For ImportMany, unwrap the collection first
        if (isImportMany)
        {
            type = GetCollectionElementType(type, state) ?? type;
        }

        // Unwrap Lazy<T>, Lazy<T, TMetadata>, ExportFactory<T>, ExportFactory<T, TMetadata>
        type = UnwrapLazyOrExportFactory(type, state);

        return type;
    }

    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol collectionType, AnalyzerState state)
    {
        // Check for array
        if (collectionType is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        // Check for IEnumerable<T>, ICollection<T>, IList<T>, List<T>, etc.
        if (collectionType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // Check if implements IEnumerable<T>
            foreach (INamedTypeSymbol iface in namedType.AllInterfaces.Prepend(namedType))
            {
                if (iface.IsGenericType &&
                    state.IEnumerableType is not null &&
                    SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, state.IEnumerableType))
                {
                    return iface.TypeArguments[0];
                }
            }
        }

        return null;
    }

    private static ITypeSymbol UnwrapLazyOrExportFactory(ITypeSymbol type, AnalyzerState state)
    {
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return type;
        }

        INamedTypeSymbol originalDef = namedType.OriginalDefinition;

        // Check for Lazy<T> or Lazy<T, TMetadata>
        if ((state.LazyType is not null && SymbolEqualityComparer.Default.Equals(originalDef, state.LazyType)) ||
            (state.LazyWithMetadataType is not null && SymbolEqualityComparer.Default.Equals(originalDef, state.LazyWithMetadataType)))
        {
            return namedType.TypeArguments[0];
        }

        // Check for ExportFactory<T> or ExportFactory<T, TMetadata>
        if ((state.ExportFactoryType is not null && SymbolEqualityComparer.Default.Equals(originalDef, state.ExportFactoryType)) ||
            (state.ExportFactoryWithMetadataType is not null && SymbolEqualityComparer.Default.Equals(originalDef, state.ExportFactoryWithMetadataType)))
        {
            return namedType.TypeArguments[0];
        }

        return type;
    }

    private static bool IsAssignableTo(ITypeSymbol source, ITypeSymbol target, Compilation compilation)
    {
        // Direct equality check
        if (SymbolEqualityComparer.Default.Equals(source, target))
        {
            return true;
        }

        // Handle target being 'object' - everything is assignable to object
        if (target.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        // Check if source inherits from target (for classes)
        if (target.TypeKind == TypeKind.Class && source is INamedTypeSymbol namedSource)
        {
            INamedTypeSymbol? currentType = namedSource.BaseType;
            while (currentType is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(currentType, target))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }
        }

        // Check if source implements target interface
        if (target.TypeKind == TypeKind.Interface && source is INamedTypeSymbol namedSourceForInterface)
        {
            foreach (INamedTypeSymbol iface in namedSourceForInterface.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record AnalyzerState(
        INamedTypeSymbol? MefV1ImportAttribute,
        INamedTypeSymbol? MefV1ImportManyAttribute,
        INamedTypeSymbol? LazyType,
        INamedTypeSymbol? LazyWithMetadataType,
        INamedTypeSymbol? ExportFactoryType,
        INamedTypeSymbol? ExportFactoryWithMetadataType,
        INamedTypeSymbol? IEnumerableType);
}
