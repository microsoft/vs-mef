// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

/// <summary>
/// Creates a diagnostic when a type with MEF exports defines non-default constructors without marking any with <c>[ImportingConstructor]</c>.
/// </summary>
/// <remarks>
/// This analyzer detects when a class has MEF export attributes (either class-level or instance member exports)
/// but only defines non-default constructors that are not annotated with <c>[ImportingConstructor]</c>.
/// MEF cannot instantiate such types because it doesn't know which constructor to use or how to satisfy the parameters.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF004ExportWithoutImportingConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF004";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    internal static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
        id: Id,
        title: Strings.VSMEF004_Title,
        messageFormat: Strings.VSMEF004_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<string> MefV1ExportAttributeNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> MefV2ExportAttributeNamespace = ImmutableArray.Create("System", "Composition");
    private static readonly ImmutableArray<string> MefV1ImportingConstructorNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> MefV2ImportingConstructorNamespace = ImmutableArray.Create("System", "Composition");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            // Only run if MEF assemblies are referenced
            bool mefV1AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase));
            bool mefV2AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.Composition.AttributedModel", StringComparison.OrdinalIgnoreCase));
            if (mefV1AttributesPresent || mefV2AttributesPresent)
            {
                context.RegisterSymbolAction(context => this.AnalyzeSymbol(context), SymbolKind.NamedType);
            }
        });
    }

    /// <summary>
    /// Analyzes a named type symbol to check if it has MEF exports but lacks appropriate constructors.
    /// </summary>
    /// <param name="context">The symbol analysis context.</param>
    private void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        INamedTypeSymbol symbol = (INamedTypeSymbol)context.Symbol;

        // Skip interfaces, delegates, enums and value types as they don't have constructors in the sense we care about
        if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract)
        {
            return;
        }

        // Check if the type has any Export attributes (instance exports, not static members)
        bool hasInstanceExports = HasInstanceExports(symbol);
        if (!hasInstanceExports)
        {
            return;
        }

        // Get all constructors
        var constructors = symbol.Constructors.Where(c => !c.IsStatic && !c.IsImplicitlyDeclared).ToList();

        // If there are no explicitly declared constructors, the compiler provides a default parameterless constructor
        if (constructors.Count == 0)
        {
            return; // Default constructor is fine
        }

        // Check if there's a parameterless constructor
        var parameterlessConstructor = constructors.FirstOrDefault(c => c.Parameters.Length == 0);
        if (parameterlessConstructor is not null)
        {
            return; // Parameterless constructor is fine, MEF can use it
        }

        // Check if any constructor has [ImportingConstructor] attribute
        bool hasImportingConstructor = constructors.Any(c => HasImportingConstructorAttribute(c));
        if (hasImportingConstructor)
        {
            return; // Has importing constructor, all good
        }

        // Found a violation: has instance exports, no default constructor, and no importing constructor
        var firstConstructor = constructors.First();
        context.ReportDiagnostic(Diagnostic.Create(
            Descriptor,
            firstConstructor.Locations.FirstOrDefault() ?? symbol.Locations.FirstOrDefault(),
            symbol.Name));
    }

    /// <summary>
    /// Determines whether the specified type has any instance-level MEF exports.
    /// </summary>
    /// <param name="symbol">The type symbol to analyze.</param>
    /// <returns><see langword="true"/> if the type has instance exports; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for export attributes on the type itself or on its instance members (properties, methods, fields).
    /// Static members with export attributes are ignored since they don't require type instantiation.
    /// </remarks>
    private static bool HasInstanceExports(INamedTypeSymbol symbol)
    {
        // Check the type itself for Export attributes
        if (HasExportAttribute(symbol))
        {
            return true;
        }

        // Check instance members (properties, methods, fields) for Export attributes
        foreach (ISymbol member in symbol.GetMembers())
        {
            // Skip static members and nested types
            if (member.IsStatic || member is ITypeSymbol)
            {
                continue;
            }

            if (HasExportAttribute(member))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified symbol has any MEF export attributes.
    /// </summary>
    /// <param name="symbol">The symbol to check for export attributes.</param>
    /// <returns><see langword="true"/> if the symbol has export attributes; otherwise, <see langword="false"/>.</returns>
    private static bool HasExportAttribute(ISymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (IsExportAttribute(attribute.AttributeClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified constructor has the ImportingConstructor attribute.
    /// </summary>
    /// <param name="constructor">The constructor to check.</param>
    /// <returns><see langword="true"/> if the constructor has the ImportingConstructor attribute; otherwise, <see langword="false"/>.</returns>
    private static bool HasImportingConstructorAttribute(IMethodSymbol constructor)
    {
        foreach (AttributeData attribute in constructor.GetAttributes())
        {
            if (IsImportingConstructorAttribute(attribute.AttributeClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified attribute type is a MEF export attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is an export attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for both MEF v1 and MEF v2 export attributes, including custom attributes that derive from the base export attribute types.
    /// </remarks>
    private static bool IsExportAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check if it's a direct Export attribute or derived from one
        return IsAttributeOfType(attributeType, "ExportAttribute", MefV1ExportAttributeNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ExportAttribute", MefV2ExportAttributeNamespace.AsSpan());
    }

    /// <summary>
    /// Determines whether the specified attribute type is a MEF importing constructor attribute.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <returns><see langword="true"/> if the attribute type is an importing constructor attribute; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method checks for both MEF v1 and MEF v2 importing constructor attributes, including custom attributes that derive from the base importing constructor attribute types.
    /// </remarks>
    private static bool IsImportingConstructorAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        // Check if it's a direct ImportingConstructor attribute or derived from one
        return IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV1ImportingConstructorNamespace.AsSpan()) ||
               IsAttributeOfType(attributeType, "ImportingConstructorAttribute", MefV2ImportingConstructorNamespace.AsSpan());
    }

    /// <summary>
    /// Determines whether the specified attribute type matches the expected type and namespace, including inheritance hierarchy.
    /// </summary>
    /// <param name="attributeType">The attribute type to check.</param>
    /// <param name="attributeTypeName">The expected attribute type name.</param>
    /// <param name="expectedNamespace">The expected namespace components.</param>
    /// <returns><see langword="true"/> if the attribute type matches; otherwise, <see langword="false"/>.</returns>
    private static bool IsAttributeOfType(INamedTypeSymbol attributeType, string attributeTypeName, ReadOnlySpan<string> expectedNamespace)
    {
        // Check the attribute type itself and its base types
        var current = attributeType;
        while (current is not null)
        {
            if (current.Name == attributeTypeName && IsNamespaceMatch(current.ContainingNamespace, expectedNamespace))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified namespace matches the expected namespace components.
    /// </summary>
    /// <param name="actual">The actual namespace to check.</param>
    /// <param name="expectedNames">The expected namespace components in reverse order (leaf to root).</param>
    /// <returns><see langword="true"/> if the namespace matches; otherwise, <see langword="false"/>.</returns>
    private static bool IsNamespaceMatch(INamespaceSymbol? actual, ReadOnlySpan<string> expectedNames)
    {
        if (actual is null or { IsGlobalNamespace: true })
        {
            return expectedNames.Length == 0;
        }

        if (expectedNames.Length == 0)
        {
            return false;
        }

        if (actual.Name != expectedNames[expectedNames.Length - 1])
        {
            return false;
        }

        return IsNamespaceMatch(actual.ContainingNamespace, expectedNames.Slice(0, expectedNames.Length - 1));
    }
}
