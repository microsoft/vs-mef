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
    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF004_Title,
        messageFormat: Strings.VSMEF004_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
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
            // Only run if MEF assemblies are referenced
            if (Utils.ReferencesMefAttributes(context.Compilation))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context), SymbolKind.NamedType);
            }
        });

        static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            INamedTypeSymbol symbol = (INamedTypeSymbol)context.Symbol;

            // Skip interfaces, delegates, enums and value types as they don't have constructors in the sense we care about, and structs always have default constructors.
            if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract)
            {
                return;
            }

            // Check if the type has any Export attributes (instance exports, not static members)
            bool hasInstanceExports = Utils.HasInstanceExports(symbol);
            if (!hasInstanceExports)
            {
                return;
            }

            // Skip types marked with [PartNotDiscoverable] as they are intended for manual construction
            if (Utils.HasPartNotDiscoverableAttribute(symbol))
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
            IMethodSymbol? parameterlessConstructor = constructors.FirstOrDefault(c => c.Parameters.Length == 0);
            if (parameterlessConstructor is not null)
            {
                return; // Parameterless constructor is fine, MEF can use it
            }

            // Check if any constructor has [ImportingConstructor] attribute
            bool hasImportingConstructor = constructors.Any(Utils.HasImportingConstructorAttribute);
            if (hasImportingConstructor)
            {
                return; // Has importing constructor, all good
            }

            // Found a violation: has instance exports, no default constructor, and no importing constructor
            IMethodSymbol firstConstructor = constructors.First();
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptor,
                firstConstructor.Locations.FirstOrDefault() ?? symbol.Locations.FirstOrDefault(),
                symbol.Name));
        }
    }
}
