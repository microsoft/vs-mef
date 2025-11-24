// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF006ImportNullabilityAnalyzer, Microsoft.VisualStudio.Composition.Analyzers.CodeFixes.VSMEF006ImportNullabilityCodeFixProvider>;

public class VSMEF006ImportNullabilityAnalyzerTests
{
    [Fact]
    public async Task ImportWithoutNullableAnnotations_NoWarning()
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
    public async Task NullableImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string? Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonNullableImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
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
    public async Task NullableImportWithoutAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NonNullableImportWithAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.AllowDefaultWithoutNullableDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NullablePropertyImportWithoutAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NullableConstructorParameterImportWithoutAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string? {|#0:value|})
                {
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ImportManyWithNullableWithoutAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string>? {|#0:Values|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Values");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MefV2NullableImportWithoutAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MefV2NonNullableImportWithAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.AllowDefaultWithoutNullableDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NullableImportWithAllowDefaultFalse_Warning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = false)]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public int Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public int? Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int? Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import]
                public int Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2NullableValueTypeImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import]
                public int? Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2NullableValueTypeImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public int? Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValueTypeConstructorParameterImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableValueTypeConstructorParameterImportWithoutAllowDefault_NoWarning()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] int? value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableImportWithoutAllowDefault_CodeFix_AddAllowDefault()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        string fixedTest = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string? Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
    }

    [Fact]
    public async Task NullableImportWithoutAllowDefault_CodeFix_MakeNonNullable()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        string fixedTest = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; } = null!;
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest, 1); // Second code fix
    }

    [Fact]
    public async Task NonNullableImportWithAllowDefault_CodeFix_MakeNullable()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string {|#0:Value|} { get; set; }
            }
            """;

        string fixedTest = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string? Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.AllowDefaultWithoutNullableDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
    }

    [Fact]
    public async Task NonNullableImportWithAllowDefault_CodeFix_RemoveAllowDefault()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string {|#0:Value|} { get; set; }
            }
            """;

        string fixedTest = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.AllowDefaultWithoutNullableDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest, 1); // Second code fix
    }

    [Fact]
    public async Task NullablePropertyImportWithoutAllowDefault_CodeFix_AddAllowDefault()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        string fixedTest = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string? Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
    }

    [Fact]
    public async Task ImportWithExistingAllowDefaultFalse_CodeFix_UpdateToTrue()
    {
        string test = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = false)]
                public string? {|#0:Value|} { get; set; }
            }
            """;

        string fixedTest = """
            #nullable enable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string? Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
    }

    [Fact]
    public async Task NullableDisabledImportWithAllowDefault_NoWarning()
    {
        string test = """
            #nullable disable
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableUnspecifiedImportWithAllowDefault_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyImportWithAllowDefault_LazyIsNullable()
    {
        string test = """
            #nullable enable
            using System;
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public Lazy<string>? Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LazyImportWithAllowDefault_LazyNotNullable_Warning()
    {
        string test = """
            #nullable enable
            using System;
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import(AllowDefault = true)]
                public Lazy<string> {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.AllowDefaultWithoutNullableDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task LazyImportNullable_WithoutAllowDefault_Warning()
    {
        string test = """
            #nullable enable
            using System;
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public Lazy<string>? {|#0:Value|} { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF006ImportNullabilityAnalyzer.NullableWithoutAllowDefaultDescriptor)
            .WithLocation(0)
            .WithArguments("Value");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}
