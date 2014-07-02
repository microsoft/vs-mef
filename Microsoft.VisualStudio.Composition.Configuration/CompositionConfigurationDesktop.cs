namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Emit;
    using Microsoft.CodeAnalysis.Text;
    using Validation;

    public static class CompositionConfigurationDesktop
    {
        public static IExportProviderFactory LoadDefault()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName.Replace(".vshost", string.Empty);
            string baseName = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath));
            string defaultCompositionFile = baseName + ".Composition.dll";
            return CompositionConfiguration.Load(Assembly.LoadFile(defaultCompositionFile));
        }

        public static async Task SaveAsync(this CompositionConfiguration configuration, string assemblyPath, string pdbPath = null, string sourceFilePath = null, TextWriter buildOutput = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            using (Stream assemblyStream = File.Open(assemblyPath, FileMode.Create))
            {
                using (Stream pdbStream = pdbPath != null ? File.Open(pdbPath, FileMode.Create) : null)
                {
                    using (FileStream sourceFile = sourceFilePath != null ? File.Open(sourceFilePath, FileMode.Create) : null)
                    {
                        var result = await configuration.SaveCompilationAsync(
                            assemblyName,
                            assemblyStream,
                            pdbStream,
                            sourceFile,
                            buildOutput,
                            cancellationToken: cancellationToken);
                        if (!result.Success)
                        {
                            throw new Exception("Internal error");
                        }
                    }
                }
            }
        }

        public static Task<EmitResult> SaveCompilationAsync(this CompositionConfiguration configuration, string assemblyName, Stream assemblyStream, Stream pdbStream = null, Stream sourceFile = null, TextWriter buildOutput = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, "configuration");
            Requires.NotNullOrEmpty(assemblyName, "assemblyName");
            Requires.NotNull(assemblyStream, "assemblyStream");

            return Task.Run(async delegate
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool debug = pdbStream != null;
                CSharpCompilation templateCompilation = CreateTemplateCompilation(assemblyName, debug);

                var compilation = await AddGeneratedCodeAndDependenciesAsync(templateCompilation, configuration, sourceFile, cancellationToken);

                var result = compilation.Emit(
                    assemblyStream,
                    pdbStream: pdbStream,
                    cancellationToken: cancellationToken);

                if (buildOutput != null)
                {
                    if (!result.Success)
                    {
                        await buildOutput.WriteLineAsync("Build failed.");
                    }

                    foreach (var diagnostic in result.Diagnostics)
                    {
                        if (diagnostic.Severity > DiagnosticSeverity.Info)
                        {
                            string fileName = sourceFile is FileStream ? Path.GetFileName(((FileStream)sourceFile).Name) : assemblyName;
                            string location = fileName;
                            if (diagnostic.Location != Location.None)
                            {
                                location += string.Format(
                                    CultureInfo.InvariantCulture,
                                    "({0},{1},{2},{3})",
                                    diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                                    diagnostic.Location.GetLineSpan().StartLinePosition.Character,
                                    diagnostic.Location.GetLineSpan().EndLinePosition.Line + 1,
                                    diagnostic.Location.GetLineSpan().EndLinePosition.Character);
                            }

                            string formattedMessage = string.Format(
                                CultureInfo.CurrentCulture,
                                "{0}: {1} {2}: {3}",
                                location,
                                diagnostic.Severity,
                                diagnostic.Id,
                                diagnostic.GetMessage());

                            await buildOutput.WriteLineAsync(formattedMessage);
                        }
                    }
                }

                return result;
            });
        }

        private static CSharpCompilation CreateTemplateCompilation(string assemblyName, bool debug)
        {
            var referenceAssemblies = ImmutableHashSet.Create(
                Assembly.GetExecutingAssembly(),
                Assembly.Load("System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                Assembly.Load("System.Reflection, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                typeof(ILazy<>).Assembly,
                typeof(Lazy<,>).Assembly,
                typeof(System.Composition.ExportFactory<>).Assembly,
                typeof(ImmutableDictionary).Assembly);

            return CSharpCompilation.Create(
                assemblyName,
                references: referenceAssemblies.Select(a => MetadataFileReferenceProvider.Default.GetReference(a.Location, MetadataReferenceProperties.Assembly)),
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimize: !debug,
                    debugInformationKind: debug ? DebugInformationKind.Full : DebugInformationKind.None,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
        }

        public static async Task<IExportProviderFactory> CreateContainerFactoryAsync(this CompositionConfiguration configuration, Stream sourceFile = null, TextWriter buildOutput = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            string assemblyName = Path.GetRandomFileName();
            var assemblyStream = new MemoryStream();

            var result = await configuration.SaveCompilationAsync(
                assemblyName,
                assemblyStream: assemblyStream,
                sourceFile: sourceFile,
                buildOutput: buildOutput);
            if (result.Success)
            {
                var compositionAssembly = Assembly.Load(assemblyStream.ToArray());
                return CompositionConfiguration.Load(compositionAssembly);
            }
            else
            {
                throw new Exception("Internal error.");
            }
        }

        private static async Task<CSharpCompilation> AddGeneratedCodeAndDependenciesAsync(CSharpCompilation compilationTemplate, CompositionConfiguration configuration, Stream sourceFile = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var templateFactory = new CompositionTemplateFactory();
            templateFactory.Configuration = configuration;
            string source = templateFactory.TransformText();

            SyntaxTree syntaxTree;
            if (sourceFile != null)
            {
                FileStream sourceFileStream = sourceFile as FileStream;
                string sourceFilePath = sourceFileStream != null ? sourceFileStream.Name : null;
                Encoding encoding = new UTF8Encoding(true);
                var writer = new StreamWriter(sourceFile, encoding);
                await writer.WriteAsync(source);
                await writer.FlushAsync();
                sourceFile.Position = 0;
                syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(sourceFile, encoding), path:  sourceFilePath ?? string.Empty, cancellationToken: cancellationToken);
            }
            else
            {
                syntaxTree = SyntaxFactory.ParseSyntaxTree(source);
            }

            var assemblies = ImmutableHashSet.Create<Assembly>()
                .Union(configuration.AdditionalReferenceAssemblies)
                .Union(templateFactory.RelevantAssemblies);
            var embeddedInteropAssemblies = CreateEmbeddedInteropAssemblies(templateFactory.RelevantEmbeddedTypes);
            return compilationTemplate
                .AddReferences(assemblies.Select(r => MetadataFileReferenceProvider.Default.GetReference(r.Location, MetadataReferenceProperties.Assembly)))
                .AddReferences(embeddedInteropAssemblies.Select(r => r.ToMetadataReference(embedInteropTypes: true)))
                .AddSyntaxTrees(syntaxTree);
        }

        private static CompilationUnitSyntax CreateTemplateEmbeddableTypesFile()
        {
            return SyntaxFactory.CompilationUnit()
                .WithUsings(
                        SyntaxFactory.List<UsingDirectiveSyntax>(
                            new[] {
                                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Runtime.CompilerServices")),
                                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Runtime.InteropServices")),
                            }))
                .WithAttributeLists(
                    SyntaxFactory.List<AttributeListSyntax>(
                        new AttributeListSyntax[]{
                            SyntaxFactory.AttributeList(
                                SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(
                                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(@"Guid"))
                                    .WithArgumentList(
                                        SyntaxFactory.AttributeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(
                                                SyntaxFactory.AttributeArgument(
                                                    SyntaxFactory.LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        SyntaxFactory.Literal(Guid.NewGuid().ToString()))))))))
                            .WithTarget(
                                SyntaxFactory.AttributeTargetSpecifier(
                                    SyntaxFactory.Token(
                                        SyntaxKind.AssemblyKeyword))),
                            SyntaxFactory.AttributeList(
                                SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(
                                    SyntaxFactory.Attribute(
                                        SyntaxFactory.IdentifierName(@"ImportedFromTypeLib"))
                                    .WithArgumentList(
                                        SyntaxFactory.AttributeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(
                                                SyntaxFactory.AttributeArgument(
                                                    SyntaxFactory.LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        SyntaxFactory.Literal(""))))))))
                            .WithTarget(
                                SyntaxFactory.AttributeTargetSpecifier(
                                    SyntaxFactory.Token(
                                        SyntaxKind.AssemblyKeyword)))
                        }));
        }

        private static MemberDeclarationSyntax DefineEmbeddableType(Type iface)
        {
            // TODO: 
            // 1. add support for interfaces that derive from PIAs that are not embedded.

            var attributes = new List<AttributeSyntax>();
            attributes.Add(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(@"ComImport")));
            attributes.Add(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(@"Guid"))
                .WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(iface.GUID.ToString())))))));
            var ifaceType = iface.GetCustomAttribute<System.Runtime.InteropServices.InterfaceTypeAttribute>();
            if (ifaceType != null)
            {
                attributes.Add(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(@"InterfaceType"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(
                                SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    SyntaxFactory.Literal((short)ifaceType.Value)))))));
            }

            return SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(iface.Namespace))
                .WithMembers(
                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        SyntaxFactory.InterfaceDeclaration(iface.Name)
                        .WithAttributeLists(SyntaxFactory.SingletonList<AttributeListSyntax>(SyntaxFactory.AttributeList().AddAttributes(attributes.ToArray())))
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))));
        }

        private static IEnumerable<CSharpCompilation> CreateEmbeddedInteropAssemblies(IEnumerable<Type> embeddedTypes)
        {
            var sourceFile = CreateTemplateEmbeddableTypesFile()
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(embeddedTypes.Select(iface => DefineEmbeddableType(iface))))
                .NormalizeWhitespace();

            var assemblies = ImmutableHashSet.Create<Assembly>(
                typeof(Guid).Assembly,
                typeof(string).Assembly);

            var compilationUnit = CSharpCompilation.Create("NoPIA")
                .AddSyntaxTrees(sourceFile.SyntaxTree)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(assemblies.Select(r => MetadataFileReferenceProvider.Default.GetReference(r.Location, MetadataReferenceProperties.Assembly)));

            var result = compilationUnit.Emit(Stream.Null);
            if (!result.Success)
            {
                throw new CompositionFailedException("Failed to generate embeddable types.");
            }

            return ImmutableList.Create(compilationUnit);
        }
    }
}
