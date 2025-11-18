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
                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndPrimaryDefaultConstructorWithoutImportingConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class {|VSMEF004:Foo|}(string parameter)
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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

                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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

                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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

                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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

                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                public {|VSMEF004:Foo|}(string parameter)
                {
                }

                public Foo(int parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndPrivateConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                private {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndInternalConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                internal {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithExportAndProtectedConstructor_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                protected {|VSMEF004:Foo|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                    public {|VSMEF004:Foo|}(string parameter)
                    {
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
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
                [ImportingConstructor]
                public Service(string config) { }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(6, 12).WithArguments("Service");
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
                [ImportingConstructor]
                public Service(string config) { }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(6, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task ClassWithExportAndNonDefaultConstructor_CodeFixAddsParameterlessConstructor()
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
                public Service()
                {
                }

                public Service(string config) { }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(6, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, codeActionIndex: 1);
    }

    [Fact]
    public async Task ClassWithMefV2ExportAndNonDefaultConstructor_CodeFixAddsParameterlessConstructor()
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
                public Service()
                {
                }

                public Service(string config) { }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(6, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, codeActionIndex: 1);
    }

    [Fact]
    public async Task ClassWithCustomMefV1ExportAndNonDefaultConstructor_CodeFixAddsCorrectImportingConstructorAttribute()
    {
        string testCode = """
            using System;
            using System.ComponentModel.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute() : base() { }
            }

            [MyExport]
            class Service
            {
                public Service(string config) { }
            }
            """;

        string fixedCode = """
            using System;
            using System.ComponentModel.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute() : base() { }
            }

            [MyExport]
            class Service
            {
                [ImportingConstructor]
                public Service(string config) { }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(13, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task ClassWithCustomMefV2ExportAndNonDefaultConstructor_CodeFixAddsCorrectImportingConstructorAttribute()
    {
        string testCode = """
            using System;
            using System.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute() : base() { }
            }

            [MyExport]
            class Service
            {
                public Service(string config) { }
            }
            """;

        string fixedCode = """
            using System;
            using System.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute() : base() { }
            }

            [MyExport]
            class Service
            {
                [ImportingConstructor]
                public Service(string config) { }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(13, 12).WithArguments("Service");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task ClassInheritingFromMefV1InheritedExportBase_Warning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [InheritedExport]
            public class BaseClass { }

            public class DerivedClass : BaseClass
            {
                public {|VSMEF004:DerivedClass|}(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassInheritingFromMefV1InheritedExportBase_CodeFixAddsCorrectImportingConstructorAttribute()
    {
        string testCode = """
            using System.ComponentModel.Composition;

            [InheritedExport]
            public class BaseClass { }

            public class DerivedClass : BaseClass
            {
                public DerivedClass(string parameter)
                {
                }
            }
            """;

        string fixedCode = """
            using System.ComponentModel.Composition;

            [InheritedExport]
            public class BaseClass { }

            public class DerivedClass : BaseClass
            {
                [ImportingConstructor]
                public DerivedClass(string parameter)
                {
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(8, 12).WithArguments("DerivedClass");
        await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task ClassInheritingFromNonInheritedExportBase_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]  // Regular Export, not InheritedExport
            public class BaseService { }

            public class DerivedService : BaseService  // Does NOT inherit the export
            {
                public DerivedService(string parameter)  // Non-default constructor is OK - not a MEF part
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassInheritingFromCustomInheritedExportDerived_Warning()
    {
        string testCode = """
            using System;
            using System.ComponentModel.Composition;

            // Custom attribute derived from InheritedExportAttribute
            [AttributeUsage(AttributeTargets.Class)]
            public class CustomInheritedExportAttribute : InheritedExportAttribute
            {
                public CustomInheritedExportAttribute() : base() { }
            }

            [CustomInheritedExport]
            public class BaseService { }

            public class DerivedService : BaseService
            {
                public {|VSMEF004:DerivedService|}(string parameter)
                {
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.ComponentModel.Composition;

            // Custom attribute derived from InheritedExportAttribute
            [AttributeUsage(AttributeTargets.Class)]
            public class CustomInheritedExportAttribute : InheritedExportAttribute
            {
                public CustomInheritedExportAttribute() : base() { }
            }

            [CustomInheritedExport]
            public class BaseService { }

            public class DerivedService : BaseService
            {
                [ImportingConstructor]
                public DerivedService(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(testCode, fixedCode);
    }
}
