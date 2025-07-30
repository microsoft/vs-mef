// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF005MultipleImportingConstructorsAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF005MultipleImportingConstructorsAnalyzerTests
{
    [Fact]
    public async Task ClassWithNoConstructors_NoWarning()
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
    public async Task ClassWithSingleImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithSingleNonImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMultipleNonImportingConstructors_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo() { }
                public Foo(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMultipleImportingConstructors_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|#0:Foo|}() { }

                [ImportingConstructor]
                public {|#1:Foo|}(string value) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithMultipleImportingConstructors_MefV2_Warning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|#0:Foo|}() { }

                [ImportingConstructor]
                public {|#1:Foo|}(string value) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithThreeImportingConstructors_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|#0:Foo|}() { }

                [ImportingConstructor]
                public {|#1:Foo|}(string value) { }

                [ImportingConstructor]
                public {|#2:Foo|}(int value) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(2).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithMultipleImportingConstructors_MixedParameterTypes_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|#0:Foo|}([Import]IService service) { }

                [ImportingConstructor]
                public {|#1:Foo|}([ImportMany]IService[] services) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithImportingConstructorAndRegularConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo() { }

                [ImportingConstructor]
                public Foo(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InheritedClass_WithMultipleImportingConstructors_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|#0:Foo|}() { }

                [ImportingConstructor]
                public {|#1:Foo|}(string value) { }
            }

            [Export]
            class Bar : Foo
            {
                public Bar() : base() { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CustomImportingConstructorAttribute_MEFv1_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            class CustomImportingConstructorAttribute : ImportingConstructorAttribute
            {
            }

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|#0:Foo|}() { }

                [CustomImportingConstructor]
                public {|#1:Foo|}(string value) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MixedMEFVersions_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;
            using MEFv2 = System.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]  // MEF v1
                public {|#0:Foo|}() { }

                [MEFv2.ImportingConstructor]  // MEF v2
                public {|#1:Foo|}(string value) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithMultipleImportingConstructors_NestedInNamespace_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyNamespace
            {
                [Export]
                class Foo
                {
                    [ImportingConstructor]
                    public {|#0:Foo|}() { }

                    [ImportingConstructor]
                    public {|#1:Foo|}(string value) { }
                }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task GenericClassWithMultipleImportingConstructors_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo<T>
            {
                [ImportingConstructor]
                public {|#0:Foo|}() { }

                [ImportingConstructor]
                public {|#1:Foo|}(T value) { }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Foo"),
        };
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}
