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
            using System.Composition;

            [Export(typeof(ITest))]
            interface ITest
            {
            }

            [Export(typeof(TestEnum))]
            enum TestEnum
            {
                Value
            }

            [Export(typeof(TestDelegate))]
            delegate void TestDelegate();
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}