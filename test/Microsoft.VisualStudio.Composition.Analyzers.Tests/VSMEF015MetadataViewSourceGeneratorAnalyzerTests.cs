// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.VisualStudio.Composition.Analyzers.CSharp;
using VerifyCS = CSharpCodeFixVerifier<Microsoft.VisualStudio.Composition.Analyzers.CSharp.VSMEF015MetadataViewSourceGeneratorAnalyzer, NoCodeFixProvider>;

public class VSMEF015MetadataViewSourceGeneratorAnalyzerTests
{
    [Fact]
    public async Task SameCompilationInterfaceWithoutMetadataView_Warns()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;

            public class Importer
            {
                [Import]
                public Lazy<object, ISameCompilationMetadataView> {|#0:MetadataExport|} { get; set; } = null!;
            }

            public interface ISameCompilationMetadataView
            {
                string A { get; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF015MetadataViewSourceGeneratorAnalyzer.SameCompilationDescriptor)
            .WithLocation(0)
            .WithArguments("ISameCompilationMetadataView");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task AttributedPartialInterface_DoesNotWarn()
    {
        string test = """
            using System;
            using System.ComponentModel.Composition;
            using Microsoft.VisualStudio.Composition;

            public class Importer
            {
                [Import]
                public Lazy<object, IGeneratedMetadataView> MetadataExport { get; set; } = null!;
            }

            [MetadataView]
            public partial interface IGeneratedMetadataView
            {
                string A { get; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DictionaryMetadata_DoesNotWarn()
    {
        string test = """
            using System;
            using System.Collections.Generic;
            using System.ComponentModel.Composition;

            public class Importer
            {
                [Import]
                public Lazy<object, IDictionary<string, object>> MetadataExport { get; set; } = null!;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ReferencedAssemblyInterfaceWithoutImplementationAttribute_Warns()
    {
        MetadataReference externalAssembly = CompileReferenceAssembly(
            """
            public interface ExternalMetadataView
            {
                string A { get; }
            }
            """,
            "ExternalMetadataViews");

        string test = """
            using System;
            using System.ComponentModel.Composition;

            public class Importer
            {
                [Import]
                public Lazy<object, ExternalMetadataView> {|#0:MetadataExport|} { get; set; } = null!;
            }
            """;

        var verifier = new VerifyCS.Test
        {
            TestCode = test,
        };
        verifier.TestState.AdditionalReferences.Add(externalAssembly);
        verifier.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(VSMEF015MetadataViewSourceGeneratorAnalyzer.ReferencedAssemblyDescriptor)
                .WithLocation(0)
                .WithArguments("ExternalMetadataView", "ExternalMetadataViews"));

        await verifier.RunAsync();
    }

    [Fact]
    public async Task ReferencedAssemblyInterfaceWithImplementationAttribute_DoesNotWarn()
    {
        MetadataReference externalAssembly = CompileReferenceAssembly(
            """
            using System.ComponentModel.Composition;
            using Microsoft.VisualStudio.Composition;

            [MetadataViewImplementation(typeof(ExternalMetadataViewImplementation))]
            public interface ExternalMetadataView
            {
                string A { get; }
            }

            public sealed class ExternalMetadataViewImplementation : MetadataView, ExternalMetadataView
            {
                public string A => this.GetMetadata<string>();
            }
            """,
            "ExternalMetadataViews");

        string test = """
            using System;
            using System.ComponentModel.Composition;

            public class Importer
            {
                [Import]
                public Lazy<object, ExternalMetadataView> MetadataExport { get; set; } = null!;
            }
            """;

        var verifier = new VerifyCS.Test
        {
            TestCode = test,
        };
        verifier.TestState.AdditionalReferences.Add(externalAssembly);

        await verifier.RunAsync();
    }

    [Fact]
    public async Task MetadataViewAttributeOnNonPartialInterface_Errors()
    {
        string test = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public interface {|#0:INonPartialMetadataView|}
            {
                string A { get; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF015MetadataViewSourceGeneratorAnalyzer.InvalidMetadataViewDescriptor)
            .WithLocation(0);

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MetadataViewAttributeOnInterfaceWithUnsupportedMembers_Errors()
    {
        string test = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface {|#0:IUnsupportedMetadataView|}
            {
                string A { get; set; }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF015MetadataViewSourceGeneratorAnalyzer.InvalidMetadataViewDescriptor)
            .WithLocation(0);

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MetadataViewAttributeInsideNonPartialContainingType_Errors()
    {
        string test = """
            using Microsoft.VisualStudio.Composition;

            public class Outer
            {
                [MetadataView]
                public partial interface IMetadataView
                {
                    string A { get; }
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic(VSMEF015MetadataViewSourceGeneratorAnalyzer.InvalidMetadataViewDescriptor)
            .WithSpan(3, 14, 3, 19);

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    private static MetadataReference CompileReferenceAssembly(string source, string assemblyName)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12)),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        EmitResult emitResult = compilation.Emit(stream);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return trustedPlatformAssemblies
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(Microsoft.VisualStudio.Composition.MetadataView).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.Composition.ImportAttribute).Assembly.Location),
            ])
            .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }
}

public sealed class NoCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray<string>.Empty;

    public override FixAllProvider? GetFixAllProvider() => null;

    public override Task RegisterCodeFixesAsync(CodeFixContext context) => Task.CompletedTask;
}
