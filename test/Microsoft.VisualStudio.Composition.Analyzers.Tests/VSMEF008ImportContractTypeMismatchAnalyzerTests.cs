// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Composition.Analyzers;
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
    public async Task ImportWithContractNameTypeMismatch_Error()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IImported { }
            interface IExported { }

            [Export("MyService", typeof(IExported))]
            internal class ExportedService : IImported { }

            [Export]
            class SomeClass
            {
                [Import("MyService", typeof(IExported))]
                internal IImported {|VSMEF008:ImportingProperty|} { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ImportWithContractNameTypeMismatch_AllowListedByContractName_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IImported { }
            interface IExported { }

            [Export("MyService", typeof(IExported))]
            internal class ExportedService : IImported { }

            [Export]
            class SomeClass
            {
                [Import("MyService", typeof(IExported))]
                internal IImported ImportingProperty { get; set; }
            }
            """;

        var verifier = new VerifyCS.Test { TestCode = test };
        verifier.TestState.AdditionalFiles.Add(
            ("vs-mef.ContractNamesAssignability.txt", SourceText.From("IImported <= MyService\n")));
        await verifier.RunAsync();
    }

    [Fact]
    public async Task ImportWithContractNameTypeMismatch_AllowListedByTypeIdentity_StillReportsWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            interface IImported { }
            interface IExported { }

            [Export("MyService", typeof(IExported))]
            internal class ExportedService : IImported { }

            [Export]
            class SomeClass
            {
                [Import("MyService", typeof(IExported))]
                internal IImported {|VSMEF008:ImportingProperty|} { get; set; }
            }
            """;

        var verifier = new VerifyCS.Test { TestCode = test };
        verifier.TestState.AdditionalFiles.Add(
            ("vs-mef.ContractNamesAssignability.txt", SourceText.From("IImported <= IExported\n")));
        await verifier.RunAsync();
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

    [Fact]
    public async Task ImportWithAllowListedAssignment_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyNS
            {
                interface IServiceBroker { }
                class SVsFullAccessServiceBroker { }

                class Foo
                {
                    [Import(typeof(SVsFullAccessServiceBroker))]
                    public IServiceBroker Value { get; set; }
                }
            }
            """;

        string allowList = "MyNS.IServiceBroker <= MyNS.SVsFullAccessServiceBroker\n";

        var verifier = new VerifyCS.Test { TestCode = test };
        verifier.TestState.AdditionalFiles.Add(
            ("vs-mef.ContractNamesAssignability.txt", SourceText.From(allowList)));
        await verifier.RunAsync();
    }

    [Fact]
    public async Task ImportWithAllowListedAssignmentWithComments_NoWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyNS
            {
                interface IServiceBroker { }
                class SVsFullAccessServiceBroker { }

                class Foo
                {
                    [Import(typeof(SVsFullAccessServiceBroker))]
                    public IServiceBroker Value { get; set; }
                }
            }
            """;

        string allowList = """
            # This is a comment
            MyNS.IServiceBroker <= MyNS.SVsFullAccessServiceBroker

            """;

        var verifier = new VerifyCS.Test { TestCode = test };
        verifier.TestState.AdditionalFiles.Add(
            ("vs-mef.ContractNamesAssignability.txt", SourceText.From(allowList)));
        await verifier.RunAsync();
    }

    [Fact]
    public async Task ImportWithUnrelatedAllowList_StillReportsWarning()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyNS
            {
                interface IServiceBroker { }
                class SVsFullAccessServiceBroker { }

                class Foo
                {
                    [Import(typeof(SVsFullAccessServiceBroker))]
                    public IServiceBroker {|VSMEF008:Value|} { get; set; }
                }
            }
            """;

        // The allow-list entry maps a different pair so the warning should still fire
        string allowList = "MyNS.IOtherService <= MyNS.SVsFullAccessServiceBroker\n";

        var verifier = new VerifyCS.Test { TestCode = test };
        verifier.TestState.AdditionalFiles.Add(
            ("vs-mef.ContractNamesAssignability.txt", SourceText.From(allowList)));
        await verifier.RunAsync();
    }

    [Fact]
    public async Task DiagnosticMessage_UsesFullyQualifiedTypeNames()
    {
        string test = """
            using System.ComponentModel.Composition;

            namespace MyCompany.Services
            {
                interface IServiceBroker { }
                class SVsServiceProvider { }

                class Foo
                {
                    [Import(typeof(SVsServiceProvider))]
                    public IServiceBroker {|#0:Value|} { get; set; }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic(VSMEF008ImportContractTypeMismatchAnalyzer.Id)
                .WithLocation(0)
                .WithArguments("MyCompany.Services.SVsServiceProvider", "MyCompany.Services.IServiceBroker"));
    }
}
