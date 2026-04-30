// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.Composition.Analyzers;

public class IDE0044ImportFieldSuppressorTests
{
    [Fact]
    public async Task FieldWithMefV1ImportAttribute_SuppressesIDE0044()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                private object someField;
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    [Fact]
    public async Task FieldWithMefV1ImportManyAttribute_SuppressesIDE0044()
    {
        string test = """
            using System.Collections.Generic;
            using System.ComponentModel.Composition;

            class Foo
            {
                [ImportMany]
                private IEnumerable<object> someField;
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    [Fact]
    public async Task FieldWithMefV2ImportAttribute_SuppressesIDE0044()
    {
        string test = """
            using System.Composition;

            class Foo
            {
                [Import]
                private object someField;
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    [Fact]
    public async Task FieldWithoutMefAttribute_DoesNotSuppressIDE0044()
    {
        string test = """
            class Foo
            {
                private object {|IDE0044:someField|};
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    [Fact]
    public async Task ReadonlyField_NoDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                private readonly object someField = null;
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    [Fact]
    public async Task ClassWithMixedFields_OnlyNonMefFieldsGetDiagnostic()
    {
        string test = """
            using System.ComponentModel.Composition;

            class Foo
            {
                [Import]
                private object mefField;

                private object {|IDE0044:regularField|};
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    [Fact]
    public async Task ClassWithMixedFields_BothVariantsOfMefNamespace()
    {
        string test = """
            using System.ComponentModel.Composition;
            using SystemComposition = System.Composition;

            class Foo
            {
                [Import]
                private object mefV1Field;

                [SystemComposition.Import]
                private object mefV2Field;

                private object {|IDE0044:regularField|};
            }
            """;

        await new Test { TestCode = test }.RunAsync();
    }

    /// <summary>
    /// A fake analyzer that produces IDE0044 for non-readonly, non-const fields,
    /// simulating IDE behavior for suppressor testing.
    /// </summary>
#pragma warning disable RS1001 // Missing DiagnosticAnalyzerAttribute - intentionally omitted for test-only class
    private sealed class FakeIDE0044Analyzer : DiagnosticAnalyzer
#pragma warning restore RS1001
    {
#pragma warning disable RS2008 // Enable analyzer release tracking - this is a test-only fake analyzer
        internal static readonly DiagnosticDescriptor Descriptor = new(
            id: "IDE0044",
            title: "Make field readonly",
            messageFormat: "Make field readonly",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
#pragma warning restore RS2008

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            var field = (IFieldSymbol)context.Symbol;
            if (!field.IsReadOnly && !field.IsConst)
            {
                Location? location = field.Locations.FirstOrDefault();
                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptor, location));
                }
            }
        }
    }

    private sealed class Test : CSharpCodeFixTest<FakeIDE0044Analyzer, EmptyCodeFixProvider, DefaultVerifier>
    {
        public Test()
        {
            this.ReferenceAssemblies = ReferencesHelper.DefaultReferences;
        }

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            yield return new FakeIDE0044Analyzer();
            yield return new IDE0044ImportFieldSuppressor();
        }
    }
}
