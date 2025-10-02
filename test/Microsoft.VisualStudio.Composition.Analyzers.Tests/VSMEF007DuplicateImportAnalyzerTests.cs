// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF007DuplicateImportAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF007DuplicateImportAnalyzerTests
{
    [Fact]
    public async Task ClassWithNoImports_NoWarning()
    {
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
    public async Task ClassWithSingleImport_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string Value { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDifferentTypeImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string StringValue { get; set; }

                [Import]
                public int IntValue { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDuplicateTypeImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string {|VSMEF007:Value1|} { get; set; }

                [Import]
                public string {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDuplicateImportsInConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:value1|}, [Import] string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMixedPropertyAndConstructorDuplicates_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string {|VSMEF007:PropertyValue|} { get; set; }

                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:constructorValue|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import("Contract1")]
                public string Value1 { get; set; }

                [Import("Contract2")]
                public string Value2 { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithSameContractNames_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import("SameContract")]
                public string {|VSMEF007:Value1|} { get; set; }

                [Import("SameContract")]
                public string {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMefV2Attributes_Warning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [Import]
                public string {|VSMEF007:Value1|} { get; set; }

                [Import]
                public string {|VSMEF007:Value2|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithDifferentTypeImports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string stringValue, [Import] int intValue)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithDifferentContractNames_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("Contract1")] string value1, [Import("Contract2")] string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithSameContractNames_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import("SameContract")] string {|VSMEF007:value1|}, [Import("SameContract")] string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithThreeDuplicateImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:value1|}, [Import] string {|VSMEF007:value2|}, [Import] string {|VSMEF007:value3|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithMixedImportAndNonImportParameters_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:importedValue1|}, string nonImportedValue, [Import] string {|VSMEF007:importedValue2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithContractTypeParameter_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(typeof(string))] object {|VSMEF007:value1|}, [Import(typeof(string))] object {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo(string value1, string value2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConstructorWithImportManyAttributes_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([ImportMany] IEnumerable<string> values1, [ImportMany] IEnumerable<int> values2)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
