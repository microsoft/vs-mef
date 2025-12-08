// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Creates a diagnostic when a disallowed MEF attribute version is used.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer is disabled by default. When enabled via .editorconfig, it can enforce
/// that only MEFv1 or MEFv2 attributes are used in a project. This helps maintain
/// consistency in projects that want to standardize on a single MEF version.
/// </para>
/// <para>
/// Configuration options in .editorconfig:
/// <code>
/// [*.cs]
/// dotnet_diagnostic.VSMEF012.severity = warning
/// dotnet_diagnostic.VSMEF012.allowed_mef_version = V2  # or V1
/// </code>
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class VSMEF012DisallowMefAttributeVersionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The ID for diagnostics reported by this analyzer.
    /// </summary>
    public const string Id = "VSMEF012";

    /// <summary>
    /// The descriptor for disallowing MEFv1 attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor DisallowV1Descriptor = new(
        id: Id,
        title: Strings.VSMEF012_Title,
        messageFormat: Strings.VSMEF012_V1Disallowed_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false);

    /// <summary>
    /// The descriptor for disallowing MEFv2 attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor DisallowV2Descriptor = new(
        id: Id,
        title: Strings.VSMEF012_Title,
        messageFormat: Strings.VSMEF012_V2Disallowed_MessageFormat,
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false);

    private static readonly ImmutableDictionary<string, string?> V1Properties = ImmutableDictionary<string, string?>.Empty.Add("AttributeVersion", "V1");

    private static readonly ImmutableDictionary<string, string?> V2Properties = ImmutableDictionary<string, string?>.Empty.Add("AttributeVersion", "V2");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DisallowV1Descriptor,
        DisallowV2Descriptor);

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

            // Register for symbol actions on types, properties, fields, and methods
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType, SymbolKind.Property, SymbolKind.Field, SymbolKind.Method);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        // Get the allowed MEF version from options
        MefVersion? allowedVersion = GetAllowedMefVersion(context.Options, context.Symbol.Locations.FirstOrDefault()?.SourceTree);

        if (allowedVersion is null)
        {
            // No restriction configured
            return;
        }

        ImmutableArray<AttributeData> attributes = context.Symbol.GetAttributes();

        // For methods, also check parameter attributes
        if (context.Symbol is IMethodSymbol method)
        {
            foreach (IParameterSymbol parameter in method.Parameters)
            {
                CheckAttributes(context, parameter.GetAttributes(), parameter.Locations.FirstOrDefault(), allowedVersion.Value);
            }
        }

        CheckAttributes(context, attributes, context.Symbol.Locations.FirstOrDefault(), allowedVersion.Value);
    }

    private static void CheckAttributes(
        SymbolAnalysisContext context,
        ImmutableArray<AttributeData> attributes,
        Location? location,
        MefVersion allowedVersion)
    {
        if (location is null)
        {
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            MefVersion? attrVersion = Utils.GetMefVersionFromAttribute(attribute.AttributeClass);

            if (attrVersion is null)
            {
                continue;
            }

            if (attrVersion != allowedVersion)
            {
                DiagnosticDescriptor descriptor = attrVersion == MefVersion.V1
                    ? DisallowV1Descriptor
                    : DisallowV2Descriptor;

                // Include the attribute version in properties so code fix can determine migration direction
                ImmutableDictionary<string, string?> properties = attrVersion == MefVersion.V1
                    ? V1Properties
                    : V2Properties;

                // Use the attribute location if available for more precise diagnostics
                Location attrLocation = GetAttributeLocation(attribute) ?? location;

                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    attrLocation,
                    properties,
                    attribute.AttributeClass?.Name ?? "Unknown"));
            }
        }
    }

    private static Location? GetAttributeLocation(AttributeData attribute)
    {
        if (attribute.ApplicationSyntaxReference is not null)
        {
            return Location.Create(
                attribute.ApplicationSyntaxReference.SyntaxTree,
                attribute.ApplicationSyntaxReference.Span);
        }

        return null;
    }

    /// <summary>
    /// The editorconfig option key for specifying which MEF version is allowed.
    /// Set to "V1" or "V2" to enforce a specific version.
    /// </summary>
    internal const string AllowedMefVersionOptionKey = "dotnet_diagnostic.VSMEF012.allowed_mef_version";

    private static MefVersion? GetAllowedMefVersion(AnalyzerOptions options, SyntaxTree? syntaxTree)
    {
        if (syntaxTree is null)
        {
            return null;
        }

        // Try to get the allowed_mef_version option
        AnalyzerConfigOptions configOptions = options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

        if (configOptions.TryGetValue(AllowedMefVersionOptionKey, out string? value))
        {
            return value?.ToUpperInvariant() switch
            {
                "V1" => MefVersion.V1,
                "V2" => MefVersion.V2,
                _ => null,
            };
        }

        return null;
    }
}
