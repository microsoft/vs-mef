// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF004ExportWithoutImportingConstructorAnalyzer, Microsoft.VisualStudio.Composition.Analyzers.CodeFixes.VSMEF004ExportWithoutImportingConstructorCodeFixProvider>;

public class VSMEF004ExportWithoutImportingConstructorAnalyzerTests
{
    [Fact]
    public async Task ClassWithoutExports_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndDefaultConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndImplicitDefaultConstructor_NoWarning()
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
    public async Task ClassWithExportAndImportingConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndImportingPrimaryConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            [method: ImportingConstructor]
            class Foo(string parameter)
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndMultipleConstructorsOneImporting_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo(string parameter)
                {
                }

                [ImportingConstructor]
                public Foo(int parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndNonDefaultConstructorWithoutImportingConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithExportAndPrimaryDefaultConstructorWithoutImportingConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class {|#0:Foo|}(string parameter)
            {
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithPropertyExportAndNonDefaultConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Export]
                public string Bar { get; set; }

                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithMethodExportAndNonDefaultConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Export]
                public string GetValue() => "test";

                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithFieldExportAndNonDefaultConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Export]
                public string Field = "test";

                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithStaticMemberExportAndNonDefaultConstructor_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Export]
                public static string StaticField = "test";

                [Export]
                public static string StaticProperty { get; set; }

                [Export]
                public static string StaticMethod() => "test";

                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMixedStaticAndInstanceExports_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Export]
                public static string StaticField = "test";

                [Export]
                public string InstanceProperty { get; set; }

                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task AbstractClass_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            abstract class Foo
            {
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Interface_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IFoo
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Enum_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            enum Foo
            {
                Value1
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Struct_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            struct Foo
            {
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MEFv2ExportAttributeWithNonDefaultConstructor_Warning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MEFv2ExportAttributeWithImportingConstructor_NoWarning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task CustomExportAttributeDerivedFromMEFv1_Warning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : ExportAttribute 
            { 
                public MyExportAttribute() : base() { }
            }

            [MyExport]
            class Foo
            {
                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CustomExportAttributeDerivedFromMEFv2_Warning()
    {
        string test = """
            using System;
            using System.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : ExportAttribute 
            { 
                public MyExportAttribute() : base() { }
            }

            [MyExport]
            class Foo
            {
                public {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CustomImportingConstructorAttributeDerivedFromMEFv1_NoWarning()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            [AttributeUsage(AttributeTargets.Constructor)]
            class MyImportingConstructorAttribute : ImportingConstructorAttribute { }

            [Export]
            class Foo
            {
                [MyImportingConstructor]
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task CustomImportingConstructorAttributeDerivedFromMEFv2_NoWarning()
    {
        string test = """
            using System.Composition;

            [Export]
            class Foo
            {
                [ImportingConstructor]
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithDefaultAndNonDefaultConstructors_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public Foo()
                {
                }

                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithMultipleNonDefaultConstructorsNoneImporting_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                public {|#0:Foo|}(string parameter)
                {
                }

                public Foo(int parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithExportAndPrivateConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                private {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithExportAndInternalConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                internal {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithExportAndProtectedConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                protected {|#0:Foo|}(string parameter)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NestedClassWithExportAndNonDefaultConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Outer
            {
                [Export]
                class Foo
                {
                    public {|#0:Foo|}(string parameter)
                    {
                    }
                }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Foo");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithoutMEFReferences_NoWarning()
    {
        string test = """
            // No MEF using statements
            class Foo
            {
                public Foo(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndNonDefaultConstructor_CodeFixAddsImportingConstructorAttribute()
    {
        string testCode = """
            using System.ComponentModel.Composition;

            [Export]
            class Service
            {
                public Service(string config) { }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            [Export]
            class Service
            {
                [System.ComponentModel.Composition.ImportingConstructor]
                public Service(string config) { }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(6, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task ClassWithMefV2ExportAndNonDefaultConstructor_CodeFixAddsImportingConstructorAttribute()
    {
        string testCode = """
            using System.Composition;

            [Export]
            class Service
            {
                public Service(string config) { }
            }
            """;

        string fixedCode = """
            using System.Composition;

            [Export]
            class Service
            {
                [System.Composition.ImportingConstructor]
                public Service(string config) { }
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(6, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    // TODO: Add test for second code action (parameterless constructor) when framework supports testing multiple code actions
}
