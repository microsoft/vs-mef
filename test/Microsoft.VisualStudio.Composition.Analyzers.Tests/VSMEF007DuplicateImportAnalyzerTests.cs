// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Composition.Analyzers;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF007DuplicateImportAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF007DuplicateImportAnalyzerTests
{
    [Fact]
    public async Task PlainClassWithoutMefAttributes_NoWarning()
    {
        string test = """
            class Foo
            {
                public string Value1 { get; set; }
                public string Value2 { get; set; }
                private int field1;
                private int field2;

                public Foo(string value1, string value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

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
    public async Task ImportingConstructorWithDuplicateImportsWithImportAttributes_Warning()
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
    public async Task ImportingConstructorWithDuplicateImportsWithoutImportAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string {|VSMEF007:value1|}, string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDuplicateImportsWithMixedImportAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:value1|}, string {|VSMEF007:value2|})
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
    public async Task ConstructorWithAllImplicitImports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import] string {|VSMEF007:importedValue1|}, string {|VSMEF007:implicitImport|}, [Import] string {|VSMEF007:importedValue2|})
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

    [Fact]
    public async Task ImportingConstructorWithMixedParameterTypesAndContracts_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string defaultImport, [Import("CustomContract")] string customImport)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameContractTypeButDifferentParameterTypes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(typeof(IService))] object {|VSMEF007:service1|}, [Import(typeof(IService))] object {|VSMEF007:service2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithSameTypeAndDifferentAttributes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(AllowDefault = true)] string {|VSMEF007:import1|}, string {|VSMEF007:import2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV2ImportingConstructorWithImplicitImports_Warning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string {|VSMEF007:value1|}, string {|VSMEF007:value2|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithImplicitAndExplicitSameContract_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string {|VSMEF007:implicitString|}, [Import(typeof(string))] object {|VSMEF007:explicitString|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorWithDifferentContractNames_NoWarning()
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
    public async Task ImportingConstructorWithImplicitAndDifferentExplicitContract_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string implicitString, [Import("CustomStringContract")] string explicitString)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
