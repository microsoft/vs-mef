// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF005MultipleImportingConstructorsAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF005MultipleImportingConstructorsAnalyzerTests
{
    [Fact]
    public async Task ClassWithMultipleImportingConstructors_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public {|VSMEF005:Foo|}() { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF005:Foo|}() { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF005:Foo|}() { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(int value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF005:Foo|}([Import]IService service) { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}([ImportMany]IService[] services) { }
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
                public {|VSMEF005:Foo|}() { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }
            }

            [Export]
            class Bar : Foo
            {
                public Bar() : base() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF005:Foo|}() { }

                [CustomImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF005:Foo|}() { }

                [MEFv2.ImportingConstructor]  // MEF v2
                public {|VSMEF005:Foo|}(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                    public {|VSMEF005:Foo|}() { }

                    [ImportingConstructor]
                    public {|VSMEF005:Foo|}(string value) { }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF005:Foo|}() { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(T value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructWithMultipleImportingConstructors_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            struct Foo
            {
                [ImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(int value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructWithMultipleImportingConstructors_MefV2_Warning()
    {
        string test = """
            using System.Composition;

            struct Foo
            {
                [ImportingConstructor]
                public {|VSMEF005:Foo|}(string value) { }

                [ImportingConstructor]
                public {|VSMEF005:Foo|}(int value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
