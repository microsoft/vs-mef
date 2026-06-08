// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.Tests;

using System;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Composition.Analyzers.CSharp;

public class MetadataViewSourceGeneratorTests
{
    [Fact]
    public void Generator_AnnotatesAttributedPartialInterfaceWithMetadataViewImplementationAttribute()
    {
        string source = """
            using System.ComponentModel;
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IPartialMetadataView
            {
                string A { get; }

                [DefaultValue("default")]
                string B { get; }
            }
            """;

        Compilation outputCompilation = RunGenerator(source).OutputCompilation;
        INamedTypeSymbol metadataViewInterface = outputCompilation.GetTypeByMetadataName("IPartialMetadataView")!;
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        INamedTypeSymbol generatedType = (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;
        AssertGeneratedMetadataViewType(generatedType);
    }

    [Fact]
    public void Generator_GeneratesForAttributedPartialInterfaceWithoutImports()
    {
        string source = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IAttributedMetadataView
            {
                string A { get; }
            }
            """;

        GeneratorTestResult result = RunGenerator(source);
        Assert.Equal(2, result.OutputCompilation.SyntaxTrees.Count());
    }

    [Fact]
    public void Generator_DoesNotGenerateForUnattributedPartialInterface()
    {
        string source = """
            using System;
            using System.ComponentModel.Composition;

            public class Importer
            {
                [Import]
                public Lazy<object, IPartialMetadataView> MetadataExport { get; set; } = null!;
            }

            public partial interface IPartialMetadataView
            {
                string A { get; }
            }
            """;

        GeneratorTestResult result = RunGenerator(source);
        Assert.Single(result.OutputCompilation.SyntaxTrees);
    }

    [Fact]
    public void Generator_SkipsAttributedInterfaceWithExistingMetadataViewImplementationAttribute()
    {
        string source = """
            using System.ComponentModel.Composition;
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            [MetadataViewImplementation(typeof(ExistingMetadataViewImplementation))]
            public partial interface IMetadataViewWithImplementation
            {
                string A { get; }
            }

            internal sealed class ExistingMetadataViewImplementation : MetadataView, IMetadataViewWithImplementation
            {
                public string A => this.GetMetadata<string>();
            }
            """;

        GeneratorTestResult result = RunGenerator(source);
        Assert.Single(result.OutputCompilation.SyntaxTrees);
    }

    [Fact]
    public void Generator_AnnotatesNestedAttributedPartialInterfaceWithinContainingType()
    {
        string source = """
            using Microsoft.VisualStudio.Composition;

            public partial class Outer
            {
                [MetadataView]
                public partial interface IPartialMetadataView
                {
                    string A { get; }
                }
            }
            """;

        Compilation outputCompilation = RunGenerator(source).OutputCompilation;
        INamedTypeSymbol metadataViewInterface = outputCompilation.GlobalNamespace.GetTypeMembers("Outer").Single().GetTypeMembers("IPartialMetadataView").Single();
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        INamedTypeSymbol generatedType = (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;
        AssertGeneratedMetadataViewType(generatedType);
        Assert.Equal("Outer", generatedType.ContainingType?.Name);
    }

    [Fact]
    public void Generator_AnnotatesNestedAttributedPartialInterfaceWithinGenericContainingType()
    {
        string source = """
            using Microsoft.VisualStudio.Composition;

            public partial class Outer<T>
                where T : class
            {
                [MetadataView]
                public partial interface IPartialMetadataView
                {
                    T A { get; }
                }
            }
            """;

        Compilation outputCompilation = RunGenerator(source).OutputCompilation;
        INamedTypeSymbol metadataViewInterface = outputCompilation.GlobalNamespace.GetTypeMembers("Outer", 1).Single().GetTypeMembers("IPartialMetadataView").Single();
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        INamedTypeSymbol generatedType = (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;
        AssertGeneratedMetadataViewType(generatedType);
        Assert.Equal("Outer", generatedType.ContainingType?.OriginalDefinition.Name);
        Assert.Equal(1, generatedType.ContainingType?.OriginalDefinition.Arity);
    }

    [Fact]
    public void Generator_AnnotatesGenericAttributedPartialInterface()
    {
        string source = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IPartialMetadataView<T>
                where T : class
            {
                T A { get; }
            }
            """;

        Compilation outputCompilation = RunGenerator(source).OutputCompilation;
        INamedTypeSymbol metadataViewInterface = outputCompilation.GetTypeByMetadataName("IPartialMetadataView`1")!;
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        INamedTypeSymbol generatedType = (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;
        AssertGeneratedMetadataViewType(generatedType);
    }

    [Fact]
    public void Generator_SkipsInterfacesWithUnsupportedMembers()
    {
        string source = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IUnsupportedMetadataView
            {
                string A { get; set; }
            }
            """;

        GeneratorTestResult result = RunGenerator(source);
        Assert.Single(result.OutputCompilation.SyntaxTrees);
    }

    [Fact]
    public void Generator_UsesSemanticDefaultValueArguments()
    {
        string source = """
            using System.ComponentModel;
            using Alias = System.DayOfWeek;
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IMetadataViewWithEnumDefault
            {
                [DefaultValue(Alias.Friday)]
                Alias Day { get; }
            }
            """;

        Compilation outputCompilation = RunGenerator(source).OutputCompilation;
        INamedTypeSymbol metadataViewInterface = outputCompilation.GetTypeByMetadataName("IMetadataViewWithEnumDefault")!;
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        INamedTypeSymbol generatedType = (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;

        IPropertySymbol property = generatedType.GetMembers("Day").OfType<IPropertySymbol>().Single();
        AttributeData defaultValueAttribute = property.GetAttributes().Single(a => a.AttributeClass?.Name == nameof(DefaultValueAttribute));
        Assert.Equal((int)DayOfWeek.Friday, defaultValueAttribute.ConstructorArguments[0].Value);
    }

    [Fact]
    public void Generator_OmitsNullableAnnotationsForCSharp7_3()
    {
        MetadataReference reference = CreateMetadataReference(
            """
            #nullable enable

            public interface IBaseMetadataView
            {
                string? NullableProperty { get; }
            }
            """,
            LanguageVersion.CSharp12,
            nullableContextOptions: NullableContextOptions.Enable);

        string source = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IAttributedMetadataView : IBaseMetadataView
            {
            }
            """;

        GeneratorTestResult result = RunGenerator(source, LanguageVersion.CSharp7_3, reference);
        string generatedSource = result.OutputCompilation.SyntaxTrees.Skip(1).Single().ToString();

        Assert.DoesNotContain("#nullable", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string?", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_EmitsNullableAnnotationsForCSharp12()
    {
        MetadataReference reference = CreateMetadataReference(
            """
            #nullable enable

            public interface IBaseMetadataView
            {
                string? NullableProperty { get; }
            }
            """,
            LanguageVersion.CSharp12,
            nullableContextOptions: NullableContextOptions.Enable);

        string source = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IAttributedMetadataView : IBaseMetadataView
            {
            }
            """;

        GeneratorTestResult result = RunGenerator(source, LanguageVersion.CSharp12, reference);
        string generatedSource = result.OutputCompilation.SyntaxTrees.Skip(1).Single().ToString();
        INamedTypeSymbol generatedType = GetGeneratedMetadataViewType(result.OutputCompilation, "IAttributedMetadataView");
        IPropertySymbol property = generatedType.GetMembers("NullableProperty").OfType<IPropertySymbol>().Single();

        Assert.Contains("#nullable enable", generatedSource, StringComparison.Ordinal);
        Assert.Equal(SpecialType.System_String, property.Type.SpecialType);
        Assert.Equal(NullableAnnotation.Annotated, property.Type.NullableAnnotation);
    }

    [Fact]
    public void Generator_EmitsNullableAnnotationsForDefaultLanguageVersion()
    {
        MetadataReference reference = CreateMetadataReference(
            """
            #nullable enable

            public interface IBaseMetadataView
            {
                string? NullableProperty { get; }
            }
            """,
            LanguageVersion.CSharp12,
            nullableContextOptions: NullableContextOptions.Enable);

        string source = """
            using Microsoft.VisualStudio.Composition;

            [MetadataView]
            public partial interface IAttributedMetadataView : IBaseMetadataView
            {
            }
            """;

        GeneratorTestResult result = RunGenerator(source, LanguageVersion.Default, reference);
        string generatedSource = result.OutputCompilation.SyntaxTrees.Skip(1).Single().ToString();
        INamedTypeSymbol generatedType = GetGeneratedMetadataViewType(result.OutputCompilation, "IAttributedMetadataView");
        IPropertySymbol property = generatedType.GetMembers("NullableProperty").OfType<IPropertySymbol>().Single();

        Assert.Contains("#nullable enable", generatedSource, StringComparison.Ordinal);
        Assert.Equal(SpecialType.System_String, property.Type.SpecialType);
        Assert.Equal(NullableAnnotation.Annotated, property.Type.NullableAnnotation);
    }

    [Fact]
    public void Generator_AnnotatesGeneratedTypeWhenMetadataViewInterfaceIsObsolete()
    {
        string source = """
            using System;
            using Microsoft.VisualStudio.Composition;

            [Obsolete("Use INextMetadataView instead", DiagnosticId = "VSMEFTEST001")]
            [MetadataView]
            public partial interface IObsoleteMetadataView
            {
                string A { get; }
            }
            """;

        Compilation outputCompilation = RunGenerator(source).OutputCompilation;
        INamedTypeSymbol metadataViewInterface = outputCompilation.GetTypeByMetadataName("IObsoleteMetadataView")!;
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        INamedTypeSymbol generatedType = (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;
        AttributeData obsoleteAttribute = generatedType.GetAttributes().Single(a => a.AttributeClass?.Name == nameof(ObsoleteAttribute));
        Assert.Equal("Use INextMetadataView instead", (string?)obsoleteAttribute.ConstructorArguments[0].Value);
        Assert.Equal("VSMEFTEST001", obsoleteAttribute.NamedArguments.Single(arg => arg.Key == nameof(ObsoleteAttribute.DiagnosticId)).Value.Value);
    }

    private static void AssertGeneratedMetadataViewType(INamedTypeSymbol generatedType)
    {
        INamedTypeSymbol definition = generatedType.OriginalDefinition;
        Assert.Equal(TypeKind.Class, definition.TypeKind);
        Assert.Equal(Accessibility.Internal, definition.DeclaredAccessibility);
        Assert.Equal(nameof(MetadataView), definition.BaseType?.Name);

        AttributeData generatedCodeAttribute = definition.GetAttributes().Single(a => a.AttributeClass?.Name == nameof(GeneratedCodeAttribute));
        string expectedVersion = typeof(MetadataViewSourceGenerator).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version!;
        Assert.Equal(expectedVersion, (string?)generatedCodeAttribute.ConstructorArguments[1].Value);
    }

    private static INamedTypeSymbol GetGeneratedMetadataViewType(Compilation outputCompilation, string metadataViewInterfaceName)
    {
        INamedTypeSymbol metadataViewInterface = outputCompilation.GetTypeByMetadataName(metadataViewInterfaceName)!;
        AttributeData implementationAttribute = metadataViewInterface.GetAttributes().Single(a => a.AttributeClass?.Name == "MetadataViewImplementationAttribute");
        return (INamedTypeSymbol)implementationAttribute.ConstructorArguments[0].Value!;
    }

    private static GeneratorTestResult RunGenerator(string source)
    {
        return RunGenerator(source, LanguageVersion.CSharp12);
    }

    private static GeneratorTestResult RunGenerator(string source, LanguageVersion languageVersion, params MetadataReference[] additionalReferences)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion)),
            ],
            references: GetMetadataReferences().Concat(additionalReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MetadataViewSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators:
            [
                generator.AsSourceGenerator(),
            ],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees[0].Options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning));
        return new GeneratorTestResult(outputCompilation);
    }

    private static MetadataReference CreateMetadataReference(string source, LanguageVersion languageVersion, NullableContextOptions nullableContextOptions)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: Guid.NewGuid().ToString("N"),
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion)),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: nullableContextOptions));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
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
                MetadataReference.CreateFromFile(typeof(MetadataView).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.Composition.ImportAttribute).Assembly.Location),
            ])
            .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private readonly record struct GeneratorTestResult(Compilation OutputCompilation);
}
