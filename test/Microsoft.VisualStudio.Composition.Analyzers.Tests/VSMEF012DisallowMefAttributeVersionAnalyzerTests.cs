// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF012DisallowMefAttributeVersionAnalyzer, Microsoft.VisualStudio.Composition.Analyzers.VSMEF012MigrateToMefV2CodeFixProvider>;

public class VSMEF012DisallowMefAttributeVersionAnalyzerTests
{
    private const string EditorConfigDisallowV1 = """

        root = true

        [*.cs]
        dotnet_diagnostic.VSMEF012.severity = warning
        dotnet_diagnostic.VSMEF012.allowed_mef_version = V2

        """;

    private const string EditorConfigDisallowV2 = """

        root = true

        [*.cs]
        dotnet_diagnostic.VSMEF012.severity = warning
        dotnet_diagnostic.VSMEF012.allowed_mef_version = V1

        """;

    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning()
    {
        string test = """
            class Foo
            {
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportWithoutConfig_NoWarning()
    {
        // By default, no MEF version is disallowed
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ExportWithoutConfig_NoWarning()
    {
        // By default, no MEF version is disallowed
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ImportWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ImportManyWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportManyWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ImportingConstructorWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportingConstructorWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1PartNotDiscoverableWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [PartNotDiscoverable]
            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1PartCreationPolicyWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [PartCreationPolicy(CreationPolicy.Shared)]
            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2SharedAttributeWithoutConfig_NoWarning()
    {
        string test = """
            using System.Composition;

            [Shared]
            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportMetadataWithoutConfig_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            [ExportMetadata("Key", "Value")]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleV1AttributesOnSameClass_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            [Export]
            [PartCreationPolicy(CreationPolicy.NonShared)]
            class Foo
            {
                [Import]
                public string SingleValue { get; set; }

                [ImportMany]
                public IEnumerable<string> MultipleValues { get; set; }

                [ImportingConstructor]
                public Foo()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleV2AttributesOnSameClass_NoWarning()
    {
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            [Export]
            [Shared]
            class Foo
            {
                [Import]
                public string SingleValue { get; set; }

                [ImportMany]
                public IEnumerable<string> MultipleValues { get; set; }

                [ImportingConstructor]
                public Foo()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1Export_WhenV2Required_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            [{|#0:Export|}]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            EditorConfigDisallowV1,
            VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
                .WithLocation(0)
                .WithArguments("ExportAttribute"));
    }

    [Fact]
    public async Task MefV2Export_WhenV1Required_Error()
    {
        string test = """
            using System.Composition;

            [{|#0:Export|}]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            EditorConfigDisallowV2,
            VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV2Descriptor)
                .WithLocation(0)
                .WithArguments("ExportAttribute"));
    }

    [Fact]
    public async Task MefV1Import_WhenV2Required_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [{|#0:Import|}]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            EditorConfigDisallowV1,
            VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
                .WithLocation(0)
                .WithArguments("ImportAttribute"));
    }

    [Fact]
    public async Task MefV1ImportMany_WhenV2Required_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [{|#0:ImportMany|}]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            EditorConfigDisallowV1,
            VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
                .WithLocation(0)
                .WithArguments("ImportManyAttribute"));
    }

    [Fact]
    public async Task MefV1ImportingConstructor_WhenV2Required_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [{|#0:ImportingConstructor|}]
                public Foo(string value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            EditorConfigDisallowV1,
            VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
                .WithLocation(0)
                .WithArguments("ImportingConstructorAttribute"));
    }

    [Fact]
    public async Task MefV2Import_WhenV1Required_Error()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [{|#0:Import|}]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            EditorConfigDisallowV2,
            VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV2Descriptor)
                .WithLocation(0)
                .WithArguments("ImportAttribute"));
    }

    /// <summary>
    /// When V2 is required, V2 attributes should not produce errors.
    /// </summary>
    [Fact]
    public async Task MefV1Export_WhenV2Required_V2Allowed()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test, EditorConfigDisallowV1);
    }

    /// <summary>
    /// When V1 is required, V1 attributes should not produce errors.
    /// </summary>
    [Fact]
    public async Task MefV2Export_WhenV1Required_V1Allowed()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test, EditorConfigDisallowV2);
    }

    [Fact]
    public async Task CodeFix_MigrateImportToV2()
    {
        string testCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [System.Composition.Import]
                public string Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
            .WithLocation(5, 6)
            .WithArguments("ImportAttribute");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, EditorConfigDisallowV1);
    }

    [Fact]
    public async Task CodeFix_MigrateExportToV2()
    {
        string testCode = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            [System.Composition.Export]
            class Foo
            {
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
            .WithLocation(3, 2)
            .WithArguments("ExportAttribute");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, EditorConfigDisallowV1);
    }

    [Fact]
    public async Task CodeFix_MigrateImportManyToV2()
    {
        string testCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [System.Composition.ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
            .WithLocation(6, 6)
            .WithArguments("ImportManyAttribute");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, EditorConfigDisallowV1);
    }

    [Fact]
    public async Task CodeFix_MigrateImportingConstructorToV2()
    {
        string testCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo(string value)
                {
                }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [System.Composition.ImportingConstructor]
                public Foo(string value)
                {
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
            .WithLocation(5, 6)
            .WithArguments("ImportingConstructorAttribute");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, EditorConfigDisallowV1);
    }

    /// <summary>
    /// The code fix only supports V1 to V2 migration, not the reverse,
    /// because MEFv2 Import/ImportMany cannot be applied to fields.
    /// </summary>
    [Fact]
    public async Task CodeFix_NoFixForV2ToV1()
    {
        string testCode = """
            using System.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV2Descriptor)
            .WithLocation(5, 6)
            .WithArguments("ImportAttribute");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, testCode, EditorConfigDisallowV2);
    }

    /// <summary>
    /// MEFv2 Import/ImportMany cannot be applied to fields, so no code fix should be offered.
    /// </summary>
    [Fact]
    public async Task CodeFix_NoFixForFieldImport()
    {
        string testCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string value;
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF012DisallowMefAttributeVersionAnalyzer.DisallowV1Descriptor)
            .WithLocation(5, 6)
            .WithArguments("ImportAttribute");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, testCode, EditorConfigDisallowV1);
    }
}
