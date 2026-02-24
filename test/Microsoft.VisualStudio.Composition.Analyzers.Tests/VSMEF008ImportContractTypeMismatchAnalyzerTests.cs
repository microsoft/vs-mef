// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.VSMEF008ImportContractTypeMismatchAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class VSMEF008ImportContractTypeMismatchAnalyzerTests
{
    [Fact]
    public async Task ImportWithIncompatibleContractType_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import(typeof(IService))]
                public string {|VSMEF008:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithContractNameAndIncompatibleType_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import("MyContract", typeof(IService))]
                public string {|VSMEF008:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithIncompatibleContractType_Error()
    {
        string test = """
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            interface IService { }

            class Foo
            {
                [ImportMany(typeof(IService))]
                public IEnumerable<string> {|VSMEF008:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithLazyWrapper_ContractTypeMismatch_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import(typeof(IService))]
                public Lazy<string> {|VSMEF008:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithExportFactoryWrapper_ContractTypeMismatch_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import(typeof(IService))]
                public ExportFactory<string> {|VSMEF008:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportManyWithLazyCollection_ContractTypeMismatch_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using System.Collections.Generic;

            interface IService { }

            class Foo
            {
                [ImportMany(typeof(IService))]
                public IEnumerable<Lazy<string>> {|VSMEF008:Values|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportingConstructorParameter_ContractTypeMismatch_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [ImportingConstructor]
                public Foo([Import(typeof(IService))] string {|VSMEF008:value|})
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportField_ContractTypeMismatch_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }

            class Foo
            {
                [Import(typeof(IService))]
                public string {|VSMEF008:value|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithLazyMetadata_ContractTypeMismatch_Error()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            interface IService { }
            interface IMetadata { }

            class Foo
            {
                [Import(typeof(IService))]
                public Lazy<string, IMetadata> {|VSMEF008:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithExportFactoryMetadata_ContractTypeMismatch_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IService { }
            interface IMetadata { }

            class Foo
            {
                [Import(typeof(IService))]
                public ExportFactory<string, IMetadata> {|VSMEF008:Value|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
