// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF011BothImportAndImportManyAnalyzer, Microsoft.VisualStudio.Composition.Analyzers.VSMEF011RemoveDuplicateImportCodeFixProvider>;

public class VSMEF011BothImportAndImportManyAnalyzerTests
{
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
    public async Task PropertyWithImportOnly_NoWarning()
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
    public async Task PropertyWithImportManyOnly_NoWarning()
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
    public async Task PropertyWithBothImportAndImportMany_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [Import]
                [ImportMany]
                public IEnumerable<string> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("Values"));
    }

    [Fact]
    public async Task FieldWithBothImportAndImportMany_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [Import]
                [ImportMany]
                public IEnumerable<string> {|#0:values|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("values"));
    }

    [Fact]
    public async Task ConstructorParameterWithBothImportAndImportMany_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import][ImportMany] IEnumerable<string> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("values"));
    }

    [Fact]
    public async Task MefV2PropertyWithBothImportAndImportMany_Error()
    {
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [Import]
                [ImportMany]
                public IEnumerable<string> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("Values"));
    }

    [Fact]
    public async Task MixedV1ImportAndV2ImportMany_Error()
    {
        string test = """
            using System.Collections.Generic;

            class Foo
            {
                [System.ComponentModel.Composition.Import]
                [System.Composition.ImportMany]
                public IEnumerable<string> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("Values"));
    }

    [Fact]
    public async Task MixedV2ImportAndV1ImportMany_Error()
    {
        string test = """
            using System.Collections.Generic;

            class Foo
            {
                [System.Composition.Import]
                [System.ComponentModel.Composition.ImportMany]
                public IEnumerable<string> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("Values"));
    }

    [Fact]
    public async Task MultiplePropertiesOneDuplicate_OnlyOneError()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [Import]
                public string SingleValue { get; set; }

                [Import]
                [ImportMany]
                public IEnumerable<string> {|#0:DuplicateValues|} { get; set; }

                [ImportMany]
                public IEnumerable<int> ManyValues { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("DuplicateValues"));
    }

    [Fact]
    public async Task PropertyWithImportOnlyAndContractName_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import("MyContract")]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithImportManyOnlyAndContractName_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany("MyContract")]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ConstructorParameterWithBothImportAndImportMany_Error()
    {
        string test = """
            using System.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import][ImportMany] IEnumerable<string> {|#0:values|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("values"));
    }

    [Fact]
    public async Task AttributesReversedOrder_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                [Import]
                public IEnumerable<string> {|#0:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
                .WithLocation(0)
                .WithArguments("Values"));
    }

    [Fact]
    public async Task CodeFix_CollectionType_RemovesImport()
    {
        // For collection types, the code fix removes [Import] and keeps [ImportMany]
        string testCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [Import]
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
            .WithLocation(8, 32)
            .WithArguments("Values");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_NonCollectionType_RemovesImportMany()
    {
        // For non-collection types (single values), the code fix removes [ImportMany] and keeps [Import]
        string testCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                [ImportMany]
                public string Value { get; set; }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
            .WithLocation(7, 19)
            .WithArguments("Value");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_ArrayType_RemovesImport()
    {
        // Arrays are collection types, so [Import] is removed and [ImportMany] is kept
        string testCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                [ImportMany]
                public string[] Values { get; set; }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                public string[] Values { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
            .WithLocation(7, 21)
            .WithArguments("Values");

        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_RemoveImportFromCombinedAttributeList()
    {
        string testCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [Import, ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportMany]
                public IEnumerable<string> Values { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
            .WithLocation(7, 32)
            .WithArguments("Values");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_CollectionConstructorParameter_RemovesImport()
    {
        // Constructor parameter with collection type removes [Import] and keeps [ImportMany]
        string testCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import][ImportMany] IEnumerable<string> values)
                {
                }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IEnumerable<string> values)
                {
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
            .WithLocation(7, 57)
            .WithArguments("values");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_NonCollectionConstructorParameter_RemovesImportMany()
    {
        // Constructor parameter with non-collection type removes [ImportMany] and keeps [Import]
        string testCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import][ImportMany] string value)
                {
                }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string value)
                {
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF011BothImportAndImportManyAnalyzer.Descriptor)
            .WithLocation(6, 44)
            .WithArguments("value");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }
}
