// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Creates a diagnostic when ImportMany is applied to a non-collection type or an unsupported collection configuration on a property or field.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer validates that [ImportMany] is applied to appropriate collection types and configurations
/// on properties and fields. It checks for:
/// - Non-collection types (e.g., a single value type instead of a collection)
/// - Lazy wrapping a collection (Lazy&lt;IEnumerable&lt;T&gt;&gt;) instead of a collection of Lazy (IEnumerable&lt;Lazy&lt;T&gt;&gt;)
/// - Properties without setters that are not pre-initialized (arrays cannot be pre-initialized).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF009ImportManyMemberCollectionTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF009";

    /// <summary>
    /// The descriptor for the non-collection type diagnostic.
    /// </summary>
    public static readonly DiagnosticDescriptor NonCollectionDescriptor = new(
        id: Id,
        title: Strings.VSMEF009_Title,
        messageFormat: Strings.VSMEF009_NonCollection_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The descriptor for setter-less property without initialization diagnostic.
    /// </summary>
    public static readonly DiagnosticDescriptor NotInitializedDescriptor = new(
        id: Id,
        title: Strings.VSMEF009_Title,
        messageFormat: Strings.VSMEF009_NotInitialized_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The descriptor for array without setter diagnostic.
    /// </summary>
    public static readonly DiagnosticDescriptor ArrayWithoutSetterDescriptor = new(
        id: Id,
        title: Strings.VSMEF009_Title,
        messageFormat: Strings.VSMEF009_ArrayWithoutSetter_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        NonCollectionDescriptor,
        NotInitializedDescriptor,
        ArrayWithoutSetterDescriptor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            if (!Utils.ReferencesMefAttributes(context.Compilation))
            {
                return;
            }

            INamedTypeSymbol? mefV1ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportManyAttribute");
            INamedTypeSymbol? mefV2ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.Composition.ImportManyAttribute");
            INamedTypeSymbol? ienumerableType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
            INamedTypeSymbol? icollectionType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
            INamedTypeSymbol? lazyType = context.Compilation.GetTypeByMetadataName("System.Lazy`1");
            INamedTypeSymbol? lazyWithMetadataType = context.Compilation.GetTypeByMetadataName("System.Lazy`2");

            if (mefV1ImportManyAttribute is null && mefV2ImportManyAttribute is null)
            {
                return;
            }

            var state = new AnalyzerState(
                mefV1ImportManyAttribute,
                mefV2ImportManyAttribute,
                ienumerableType,
                icollectionType,
                lazyType,
                lazyWithMetadataType);

            context.RegisterSymbolAction(ctx => AnalyzeProperty(ctx, state), SymbolKind.Property);
            context.RegisterSymbolAction(ctx => AnalyzeField(ctx, state), SymbolKind.Field);
            context.RegisterSymbolAction(ctx => AnalyzeMethod(ctx, state), SymbolKind.Method);
        });
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, AnalyzerState state)
    {
        var property = (IPropertySymbol)context.Symbol;

        if (!HasImportManyAttribute(property.GetAttributes(), state))
        {
            return;
        }

        Location? location = property.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        // Check if it's a valid collection type
        if (!IsValidCollectionType(property.Type, state, out bool isLazyWrappingCollection))
        {
            if (isLazyWrappingCollection)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NonCollectionDescriptor,
                    location,
                    property.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    Strings.VSMEF009_LazyWrappingCollection_Detail));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NonCollectionDescriptor,
                    location,
                    property.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    Strings.VSMEF009_NotCollectionType_Detail));
            }

            return;
        }

        // If property has a setter, it's fine
        if (property.SetMethod is not null)
        {
            return;
        }

        // Property without setter - check if it's an array (which can't be pre-initialized for ImportMany)
        if (property.Type is IArrayTypeSymbol)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ArrayWithoutSetterDescriptor,
                location,
                property.Name));
            return;
        }

        // Property without setter - check if the type implements ICollection<T> for Clear/Add support
        if (!ImplementsICollection(property.Type, state))
        {
            // Interface types like IEnumerable<T> that don't implement ICollection<T> can't be pre-initialized
            context.ReportDiagnostic(Diagnostic.Create(
                NotInitializedDescriptor,
                location,
                property.Name,
                Strings.VSMEF009_MustImplementICollection_Detail));
            return;
        }

        // Check if the property is definitely initialized
        if (!IsPropertyDefinitelyInitialized(property, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotInitializedDescriptor,
                location,
                property.Name,
                Strings.VSMEF009_AddInitializer_Detail));
        }
    }

    private static void AnalyzeField(SymbolAnalysisContext context, AnalyzerState state)
    {
        var field = (IFieldSymbol)context.Symbol;

        if (!HasImportManyAttribute(field.GetAttributes(), state))
        {
            return;
        }

        Location? location = field.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        // Check if it's a valid collection type
        if (!IsValidCollectionType(field.Type, state, out bool isLazyWrappingCollection))
        {
            if (isLazyWrappingCollection)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NonCollectionDescriptor,
                    location,
                    field.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    Strings.VSMEF009_LazyWrappingCollection_Detail));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NonCollectionDescriptor,
                    location,
                    field.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    Strings.VSMEF009_NotCollectionType_Detail));
            }
        }

        // Fields are always assignable (assuming not readonly, but MEF would handle that)
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
            if (!HasImportManyAttribute(parameter.GetAttributes(), state))
            {
                continue;
            }

            Location? location = parameter.Locations.FirstOrDefault();
            if (location is null)
            {
                continue;
            }

            // Check if it's a valid collection type
            if (!IsValidCollectionType(parameter.Type, state, out bool isLazyWrappingCollection))
            {
                if (isLazyWrappingCollection)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        NonCollectionDescriptor,
                        location,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                        Strings.VSMEF009_LazyWrappingCollection_Detail));
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        NonCollectionDescriptor,
                        location,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                        Strings.VSMEF009_NotCollectionType_Detail));
                }
            }

            // Note: Constructor parameter collection type restrictions are checked by VSMEF010
        }
    }

    private static bool HasImportManyAttribute(ImmutableArray<AttributeData> attributes, AnalyzerState state)
    {
        foreach (AttributeData attr in attributes)
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV1ImportManyAttribute) ||
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV2ImportManyAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidCollectionType(ITypeSymbol type, AnalyzerState state, out bool isLazyWrappingCollection)
    {
        isLazyWrappingCollection = false;

        // Check for Lazy<T> wrapping a collection (invalid pattern)
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            INamedTypeSymbol originalDef = namedType.OriginalDefinition;
            if ((state.LazyType is not null && SymbolEqualityComparer.Default.Equals(originalDef, state.LazyType)) ||
                (state.LazyWithMetadataType is not null && SymbolEqualityComparer.Default.Equals(originalDef, state.LazyWithMetadataType)))
            {
                // It's a Lazy<T> - check if T is a collection
                ITypeSymbol innerType = namedType.TypeArguments[0];
                if (IsCollectionType(innerType, state))
                {
                    isLazyWrappingCollection = true;
                    return false;
                }

                // Lazy<T> where T is not a collection is just not a collection
                return false;
            }
        }

        return IsCollectionType(type, state);
    }

    private static bool IsCollectionType(ITypeSymbol type, AnalyzerState state)
    {
        // Arrays are collections
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        // String implements IEnumerable<char> but is NOT a valid ImportMany collection type
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        // Check for IEnumerable<T> implementation
        if (type is INamedTypeSymbol namedType)
        {
            // Check if it's directly IEnumerable<T> or implements it
            foreach (INamedTypeSymbol iface in namedType.AllInterfaces.Prepend(namedType))
            {
                if (iface.IsGenericType &&
                    state.IEnumerableType is not null &&
                    SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, state.IEnumerableType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ImplementsICollection(ITypeSymbol type, AnalyzerState state)
    {
        if (state.ICollectionType is null)
        {
            return false;
        }

        if (type is INamedTypeSymbol namedType)
        {
            foreach (INamedTypeSymbol iface in namedType.AllInterfaces.Prepend(namedType))
            {
                if (iface.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, state.ICollectionType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPropertyDefinitelyInitialized(IPropertySymbol property, Compilation compilation)
    {
        // Check for property initializer
        foreach (SyntaxReference syntaxRef in property.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = syntaxRef.GetSyntax();

            // Look for an initializer in the syntax
            // This works for both C# and VB property initializers
            foreach (SyntaxNode child in syntax.DescendantNodes())
            {
                // Check if there's an EqualsValueClauseSyntax (C#) or equivalent
                // We use a string-based check to avoid language-specific dependencies
                if (child.GetType().Name.Contains("EqualsValueClause") ||
                    child.GetType().Name.Contains("AsNewClause"))
                {
                    return true;
                }
            }
        }

        // Check if assigned in all constructors
        INamedTypeSymbol? containingType = property.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        // Get all instance constructors
        IEnumerable<IMethodSymbol> constructors = containingType.InstanceConstructors
            .Where(c => !c.IsImplicitlyDeclared);

        if (!constructors.Any())
        {
            // No explicit constructors, property must have an initializer (which we already checked)
            return false;
        }

        // Find constructors that don't chain to this(...)
        List<IMethodSymbol> nonChainingConstructors = new();
        foreach (IMethodSymbol ctor in constructors)
        {
            if (!ChainsToThis(ctor))
            {
                nonChainingConstructors.Add(ctor);
            }
        }

        if (nonChainingConstructors.Count == 0)
        {
            // All constructors chain - this is unusual, but we can't easily analyze
            return false;
        }

        // Check if all non-chaining constructors assign the property
        foreach (IMethodSymbol ctor in nonChainingConstructors)
        {
            if (!ConstructorAssignsProperty(ctor, property, compilation))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ChainsToThis(IMethodSymbol constructor)
    {
        foreach (SyntaxReference syntaxRef in constructor.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = syntaxRef.GetSyntax();

            // Look for constructor initializer that calls this(...)
            foreach (SyntaxNode node in syntax.DescendantNodes())
            {
                // Check for ConstructorInitializerSyntax with ThisConstructorInitializer
                string typeName = node.GetType().Name;
                if (typeName.Contains("ConstructorInitializer"))
                {
                    // Check if it's a "this" call vs "base" call
                    string nodeText = node.ToString();
                    if (nodeText.TrimStart().StartsWith("this", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool ConstructorAssignsProperty(IMethodSymbol constructor, IPropertySymbol property, Compilation compilation)
    {
        foreach (SyntaxReference syntaxRef in constructor.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = syntaxRef.GetSyntax();

            // We need the semantic model to resolve IOperation from syntax nodes.
            // This is necessary to determine if an assignment targets our specific property.
#pragma warning disable RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer
            SemanticModel? model = compilation.GetSemanticModel(syntax.SyntaxTree);
#pragma warning restore RS1030

            // Walk the constructor body looking for assignments to the property
            foreach (SyntaxNode node in syntax.DescendantNodes())
            {
                // Look for assignment expressions
                IOperation? operation = model.GetOperation(node);
                if (operation is ISimpleAssignmentOperation assignment)
                {
                    // Check if the target is our property
                    if (assignment.Target is IPropertyReferenceOperation propRef &&
                        SymbolEqualityComparer.Default.Equals(propRef.Property, property))
                    {
                        // Check if it's assigning to 'this'
                        if (propRef.Instance is IInstanceReferenceOperation instanceRef &&
                            instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private sealed record AnalyzerState(
        INamedTypeSymbol? MefV1ImportManyAttribute,
        INamedTypeSymbol? MefV2ImportManyAttribute,
        INamedTypeSymbol? IEnumerableType,
        INamedTypeSymbol? ICollectionType,
        INamedTypeSymbol? LazyType,
        INamedTypeSymbol? LazyWithMetadataType);
}
