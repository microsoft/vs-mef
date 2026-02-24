// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF003ExportTypeMismatchAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = VisualBasicCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF003ExportTypeMismatchAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF003ExportTypeMismatchAnalyzerTests
{
    [Fact]
    public async Task ExportingClassItself_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            [Export(typeof(Test))]
            class Test
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingImplementedInterface_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest
            {
            }

            [Export(typeof(ITest))]
            class Test : ITest
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingImplementedMultiTypeInterface_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            class TestType1
            {
            }

            class TestType2
            {
            }

            interface ITest<T,V> where T : class where V : class
            {
            }

            [Export(typeof(ITest<TestType1,TestType2>))]
            class Test : ITest<TestType1,TestType2>
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingImplementedGenericTypeInterface_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest<T> where T : class
            {
            }

            [Export(typeof(ITest<>))]
            class Test<T> : ITest<T> where T : class
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingImplementedMultiGenericTypeInterface_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest<T,V> where T : class where V : class
            {
            }

            [Export(typeof(ITest<,>))]
            class Test<A,B> : ITest<A,B> where A : class where B : class
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingBaseClass_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            class BaseTest
            {
            }

            [Export(typeof(BaseTest))]
            class Test : BaseTest
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingUnimplementedInterface_ProducesDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest
            {
            }

            [Export(typeof(ITest))]
            class [|Test|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingUnrelatedClass_ProducesDiagnostic()
    {
        string test = """
            using System.Composition;

            class OtherClass
            {
            }

            [Export(typeof(OtherClass))]
            class [|Test|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingUnimplementedInterface_ProducesDiagnostic_VB()
    {
        string test = """
            Imports System.Composition

            Interface ITest
            End Interface

            <Export(GetType(ITest))>
            Class [|Test|]
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportWithoutTypeArgument_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            [Export]
            class Test
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportWithContractName_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            [Export("SomeContract")]
            class Test
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportingUnimplementedInterface_ProducesDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            [Export(typeof(ITest))]
            class [|Test|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportingImplementedInterface_ProducesNoDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            [Export(typeof(ITest))]
            class Test : ITest
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleExportsWithMismatch_ProducesDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest
            {
            }

            interface IOther
            {
            }

            [Export(typeof(ITest))]
            [Export(typeof(IOther))]
            class [|Test|] : ITest
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonClassTypes_ProduceNoDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            class ITest
            {
                [Export(typeof(ITest))]
                public ITest TestProperty => throw new System.NotImplementedException();

                [Export("Method")]
                public void TestMethod() => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyExportingMatchingType_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest
            {
            }

            class TestClass
            {
                [Export(typeof(ITest))]
                public ITest TestProperty => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyExportingCompatibleType_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest
            {
            }

            class ConcreteTest : ITest
            {
            }

            class TestClass
            {
                [Export(typeof(ITest))]
                public ConcreteTest TestProperty => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyExportingIncompatibleType_ProducesDiagnostic()
    {
        string test = """
            using System.Composition;

            interface ITest
            {
            }

            interface IOther
            {
            }

            class TestClass
            {
                [Export(typeof(ITest))]
                public IOther [|TestProperty|] => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyExportingUnrelatedClass_ProducesDiagnostic()
    {
        string test = """
            using System.Composition;

            class TestType
            {
            }

            class OtherType
            {
            }

            class TestClass
            {
                [Export(typeof(TestType))]
                public OtherType [|TestProperty|] => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1PropertyExportingIncompatibleType_ProducesDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            interface IOther
            {
            }

            class TestClass
            {
                [Export(typeof(ITest))]
                public IOther [|TestProperty|] => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyExportWithoutTypeArgument_ProducesNoDiagnostic()
    {
        string test = """
            using System.Composition;

            class TestClass
            {
                [Export]
                public string TestProperty => throw new System.NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ExportingWrongUnboundGenericWithNonGenericInterfaces_ForcesEvaluationOfAllInterfaces()
    {
        // Reproduces a bug seen related to unbound generic types.
        string test = """
            using System;
            using System.Composition;

            interface IValue<T>
            {
            }

            interface IOther<T>
            {
            }

            interface IFirstInterface
            {
            }

            [Export(typeof(IOther<>))]
            class [|Test|]<T> : IValue<T>, IFirstInterface, IDisposable
            {
                public void Dispose() => throw new NotImplementedException();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportWithContractNameAndUnimplementedType_ProducesDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            [Export("SomeContract", typeof(ITest))]
            class [|Test|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MefV1ExportWithContractNameAndImplementedType_ProducesNoDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            [Export("SomeContract", typeof(ITest))]
            class Test : ITest
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task CustomExportAttributeWithUnimplementedType_ProducesDiagnostic()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute(Type contractType) : base(contractType) { }
            }

            [MyExport(typeof(ITest))]
            class [|Test|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task CustomExportAttributeWithImplementedType_ProducesNoDiagnostic()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface ITest
            {
            }

            class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute(Type contractType) : base(contractType) { }
            }

            [MyExport(typeof(ITest))]
            class Test : ITest
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
