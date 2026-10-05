// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;
using Microsoft.VisualStudio.Composition.Analyzers;

public class CS0649ImportingMemberSuppressorTests
{
    [Fact]
    public async Task ExportedTypeWithAllowDefaultImportField_SuppressesCS0649()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System;
            using System.ComponentModel.Composition;

            [Export]
            public class SomeService
            {
                [Import(AllowDefault = true)]
                private Lazy<IService>? {|#0:someOtherService|};

                public Lazy<IService>? Value => this.someOtherService;
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs0649Diagnostic(0, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task NonImportField_DoesNotSuppressCS0649()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591

            public class SomeService
            {
                private object? {|#0:someOtherService|};

                public object? Value => this.someOtherService;
            }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs0649Diagnostic(0) },
        }.RunAsync();
    }

    [Fact]
    public async Task VisualBasicImportField_DoesNotProduceEquivalentCompilerWarning()
    {
        string test = """
            Imports System
            Imports System.ComponentModel.Composition

            <Export>
            Public Class SomeService
                <Import(AllowDefault:=True)>
                Private someOtherService As Lazy(Of IService)

                Public ReadOnly Property Value As Lazy(Of IService)
                    Get
                        Return Me.someOtherService
                    End Get
                End Property
            End Class

            Public Interface IService
            End Interface
            """;

        await new VisualBasicTest
        {
            TestCode = test,
        }.RunAsync();
    }

    private static DiagnosticResult Cs0649Diagnostic(int location, bool isSuppressed = false)
    {
        DiagnosticResult result = new DiagnosticResult("CS0649", DiagnosticSeverity.Warning)
            .WithLocation(location)
            .WithArguments("SomeService.someOtherService", "null");
        return isSuppressed ? result.WithIsSuppressed(true) : result;
    }

#pragma warning disable RS1001 // Missing DiagnosticAnalyzerAttribute - intentionally omitted for test-only class
    private sealed class NoopAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1001
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray<DiagnosticDescriptor>.Empty;

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }

    private sealed class Test : CSharpCodeFixTest<NoopAnalyzer, EmptyCodeFixProvider, DefaultVerifier>
    {
        public Test()
        {
            this.ReferenceAssemblies = ReferencesHelper.DefaultReferences;
            this.CompilerDiagnostics = CompilerDiagnostics.All;
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            yield return new NoopAnalyzer();
            yield return new CS0649ImportingMemberSuppressor();
        }
    }

    private sealed class VisualBasicTest : VisualBasicCodeFixTest<NoopAnalyzer, EmptyCodeFixProvider, DefaultVerifier>
    {
        public VisualBasicTest()
        {
            this.ReferenceAssemblies = ReferencesHelper.DefaultReferences;
            this.CompilerDiagnostics = CompilerDiagnostics.All;
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            yield return new NoopAnalyzer();
        }
    }
}
