// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.Composition.Analyzers;

public class CS8618ImportingMemberSuppressorTests
{
    [Fact]
    public async Task ExportedTypeWithImportProperty_SuppressesCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.ComponentModel.Composition;

            [Export]
            public class SomeService
            {
                [Import]
                public IService SomeOtherService { get; set; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 9, 21, 9, 37, 9, 21, 9, 37, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task ExportedMemberWithImportPropertyAndExplicitConstructor_SuppressesCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.Composition;

            public class SomeService
            {
                [Import]
                public IService SomeOtherService { get; set; }

                [Export]
                public IService ExportedService => this.SomeOtherService;

                public SomeService()
                {
                }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 13, 12, 13, 23, 8, 21, 8, 37, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task ExportedTypeWithImportManyProperty_SuppressesCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.Collections.Generic;
            using System.ComponentModel.Composition;

            [Export]
            public class SomeService
            {
                [ImportMany]
                public IEnumerable<IService> SomeOtherServices { get; set; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherServices", 10, 34, 10, 51, 10, 34, 10, 51, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task InheritedExportedTypeWithImportProperty_SuppressesCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.ComponentModel.Composition;

            [InheritedExport]
            public class SomeService
            {
                [Import]
                public IService SomeOtherService { get; set; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 9, 21, 9, 37, 9, 21, 9, 37, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task DerivedExportAndImportAttributes_SuppressCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System;
            using System.ComponentModel.Composition;

            public class MyExportAttribute : ExportAttribute
            {
                public MyExportAttribute()
                    : base(typeof(IService))
                {
                }
            }

            [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
            public class MyImportAttribute : ImportAttribute
            {
            }

            [MyExport]
            public class SomeService : IService
            {
                [MyImport]
                public IDependency Dependency { get; set; }

                public SomeService()
                {
                }
            }

            public interface IService { }

            public interface IDependency { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "Dependency", 25, 12, 25, 23, 23, 24, 23, 34, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task ExportedTypeWithImportField_SuppressesCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591, CS0169
            using System.ComponentModel.Composition;

            [Export]
            public class SomeService
            {
                [Import]
                private IService someOtherService;

                public SomeService()
                {
                }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("field", "someOtherService", 11, 12, 11, 23, 9, 22, 9, 38, isSuppressed: true) },
        }.RunAsync();
    }

    [Fact]
    public async Task AllowDefaultImport_DoesNotSuppressCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.ComponentModel.Composition;

            [Export]
            public class SomeService
            {
                [Import(AllowDefault = true)]
                public IService SomeOtherService { get; set; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 9, 21, 9, 37, 9, 21, 9, 37) },
        }.RunAsync();
    }

    [Fact]
    public async Task NonExportedType_DoesNotSuppressCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.ComponentModel.Composition;

            public class SomeService
            {
                [Import]
                public IService SomeOtherService { get; set; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 8, 21, 8, 37, 8, 21, 8, 37) },
        }.RunAsync();
    }

    [Fact]
    public async Task PartNotDiscoverableExport_DoesNotSuppressCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System.ComponentModel.Composition;

            [Export, PartNotDiscoverable]
            public class SomeService
            {
                [Import]
                public IService SomeOtherService { get; set; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 9, 21, 9, 37, 9, 21, 9, 37) },
        }.RunAsync();
    }

    [Fact]
    public async Task DerivedImportAttributeOnGetOnlyProperty_DoesNotSuppressCS8618()
    {
        string test = """
            #nullable enable
            #pragma warning disable CS1591
            using System;
            using System.ComponentModel.Composition;

            [AttributeUsage(AttributeTargets.Property)]
            public class MyImportAttribute : ImportAttribute
            {
            }

            [Export]
            public class SomeService
            {
                [MyImport]
                public IService SomeOtherService { get; }
            }

            public interface IService { }
            """;

        await new Test
        {
            TestCode = test,
            ExpectedDiagnostics = { Cs8618Diagnostic("property", "SomeOtherService", 15, 21, 15, 37, 15, 21, 15, 37) },
        }.RunAsync();
    }

    private static DiagnosticResult Cs8618Diagnostic(
        string memberKind,
        string memberName,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        int additionalStartLine,
        int additionalStartColumn,
        int additionalEndLine,
        int additionalEndColumn,
        bool isSuppressed = false)
    {
        DiagnosticResult result = new DiagnosticResult("CS8618", DiagnosticSeverity.Warning)
            .WithSpan(startLine, startColumn, endLine, endColumn)
            .WithSpan(additionalStartLine, additionalStartColumn, additionalEndLine, additionalEndColumn)
            .WithArguments(memberKind, memberName);
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
            yield return new CS8618ImportingMemberSuppressor();
        }
    }
}
