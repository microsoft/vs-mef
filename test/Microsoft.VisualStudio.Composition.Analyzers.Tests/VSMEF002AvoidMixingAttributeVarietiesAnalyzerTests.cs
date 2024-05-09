// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF002AvoidMixingAttributeVarietiesAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF002AvoidMixingAttributeVarietiesAnalyzerTests
{
    [Fact]
    public async Task NoMixing()
    {
        string test = """
            using System.ComponentModel.Composition;

            [Export]
            class Foo
            {
                [Import]
                public object Bar { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoMixing_WithMixedNestedType()
    {
        string test = """
            using MEFv1 = System.ComponentModel.Composition;
            using MEFv2 = System.Composition;
            
            [MEFv1.Export]
            class Foo
            {
                [MEFv1.Import]
                public object Bar { get; set; }

                [MEFv2.Export]
                class Baz
                {
                    [MEFv2.Import]
                    public object Qux { get; set; }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixingDirect()
    {
        string test = """
            using MEFv1 = System.ComponentModel.Composition;
            using MEFv2 = System.Composition;

            [MEFv1.Export]
            class [|Foo|]
            {
                [MEFv2.Import]
                public object Bar { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixingIndirect()
    {
        string test = """
            using System;
            using MEFv1 = System.ComponentModel.Composition;
            using MEFv2 = System.Composition;

            [AttributeUsage(AttributeTargets.Class)]
            class MyExportAttribute : MEFv1.ExportAttribute { }

            [MyExport]
            class [|Foo|]
            {
                [MEFv2.Import]
                public object Bar { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
