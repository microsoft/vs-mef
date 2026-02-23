// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when ImportMany is used with an unsupported collection type in a constructor parameter.
/// </summary>
/// <remarks>
/// <para>
/// Constructor parameters with [ImportMany] only support T[] (arrays) and IEnumerable&lt;T&gt;.
/// Other collection types like List&lt;T&gt;, IList&lt;T&gt;, ICollection&lt;T&gt;, etc. are not supported
/// because MEF must create the collection instance to pass to the constructor.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF010ImportManyParameterCollectionTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF010";

    /// <summary>
    /// The descriptor used for diagnostics created by this rule.
    /// </summary>
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: Strings.VSMEF010_Title,
        messageFormat: Strings.VSMEF010_MessageFormat,
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
            if (!Utils.ReferencesMefAttributes(context.Compilation))
            {
                return;
            }

            INamedTypeSymbol? mefV1ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.Composition.ImportManyAttribute");
            INamedTypeSymbol? mefV2ImportManyAttribute = context.Compilation.GetTypeByMetadataName("System.Composition.ImportManyAttribute");
            INamedTypeSymbol? ienumerableType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");

            if (mefV1ImportManyAttribute is null && mefV2ImportManyAttribute is null)
            {
                return;
            }

            var state = new AnalyzerState(
                mefV1ImportManyAttribute,
                mefV2ImportManyAttribute,
                ienumerableType);

            context.RegisterSymbolAction(ctx => AnalyzeMethod(ctx, state), SymbolKind.Method);
        });
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

            // Check if the collection type is supported for constructor parameters
            if (!IsSupportedConstructorCollectionType(parameter.Type, state))
            {
                Location? location = parameter.Locations.FirstOrDefault();
                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Descriptor,
                        location,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                        parameter.Name));
                }
            }
        }
    }

    private static bool HasImportManyAttribute(ImmutableArray<AttributeData> attributes, AnalyzerState state)
    {
        foreach (AttributeData attr in attributes)
        {
            // Only MEFv1 has this restriction. MEFv2 supports various collection types.
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, state.MefV1ImportManyAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedConstructorCollectionType(ITypeSymbol type, AnalyzerState state)
    {
        // Arrays are supported
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        // IEnumerable<T> is supported (exact match only, not derived types)
        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            state.IEnumerableType is not null &&
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, state.IEnumerableType))
        {
            return true;
        }

        return false;
    }

    private sealed record AnalyzerState(
        INamedTypeSymbol? MefV1ImportManyAttribute,
        INamedTypeSymbol? MefV2ImportManyAttribute,
        INamedTypeSymbol? IEnumerableType);
}
