// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF002AvoidMixingAttributeVarietiesAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "VSMEF002";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF002_Title,
        messageFormat: Strings.VSMEF002_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<string> Mefv1AttributeNamespace = ImmutableArray.Create("System", "ComponentModel", "Composition");
    private static readonly ImmutableArray<string> Mefv2AttributeNamespace = ImmutableArray.Create("System", "Composition");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            // No need to scan attributes if the compilation doesn't reference both assemblies.
            // Even in the case of custom MEF attributes being defined in another assembly,
            // the C# compiler requires references to the assemblies that declare the base type of an attribute when the attribute is used.
            bool mefV1AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.ComponentModel.Composition", StringComparison.OrdinalIgnoreCase));
            bool mefV2AttributesPresent = context.Compilation.ReferencedAssemblyNames.Any(i => string.Equals(i.Name, "System.Composition.AttributedModel", StringComparison.OrdinalIgnoreCase));
            if (mefV1AttributesPresent && mefV2AttributesPresent)
            {
                context.RegisterSymbolAction(context => this.AnalyzeSymbol(context), SymbolKind.NamedType);
            }
        });
    }

    private void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        INamedTypeSymbol symbol = (INamedTypeSymbol)context.Symbol;
        List<ISymbol>? mefv1AttributedMembers = null;
        List<ISymbol>? mefv2AttributedMembers = null;

        SearchSymbolForAttributes(symbol);
        foreach (ISymbol member in symbol.GetMembers())
        {
            // We're not interested in nested types when scanning for members of the type.
            if (member is ITypeSymbol)
            {
                continue;
            }

            SearchSymbolForAttributes(member);
        }

        if (mefv1AttributedMembers is { Count: > 0 } && mefv2AttributedMembers is { Count: > 0 })
        {
            // For additional locations, we'll use the shorter of the two lists, optimizing the assumption that the mistake is smaller than the intent.
            List<ISymbol> smallerList = mefv1AttributedMembers.Count < mefv2AttributedMembers.Count ? mefv1AttributedMembers : mefv2AttributedMembers;
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, symbol.Locations[0], smallerList.Select(s => s.Locations[0]), symbol.Name));
        }

        void SearchSymbolForAttributes(ISymbol symbol)
        {
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                CheckAttribute(symbol, attribute, ref mefv1AttributedMembers, Mefv1AttributeNamespace.AsSpan());
                CheckAttribute(symbol, attribute, ref mefv2AttributedMembers, Mefv2AttributeNamespace.AsSpan());
            }
        }
    }

    private static void CheckAttribute(ISymbol symbol, AttributeData attribute, ref List<ISymbol>? list, ReadOnlySpan<string> ns)
    {
        if (IsSymbolInOrDerivedFromNamespace(attribute.AttributeClass, ns))
        {
            list ??= new();
            list.Add(symbol);
        }
    }

    private static bool IsSymbolInOrDerivedFromNamespace(INamedTypeSymbol? attribute, ReadOnlySpan<string> namespaceName)
    {
        if (attribute is null)
        {
            return false;
        }

        if (IsNamespaceMatch(attribute.ContainingNamespace, namespaceName))
        {
            return true;
        }

        return IsSymbolInOrDerivedFromNamespace(attribute.BaseType, namespaceName);
    }

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
