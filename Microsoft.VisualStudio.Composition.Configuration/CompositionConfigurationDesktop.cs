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
    using System.Runtime.InteropServices;
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
            return Load(Assembly.LoadFile(defaultCompositionFile));
        }

        public static async Task SaveAsync(this CompositionConfiguration configuration, string assemblyPath, string pdbPath = null, string sourceFilePath = null, TextWriter buildOutput = null, bool debug = false, CancellationToken cancellationToken = default(CancellationToken))
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
                            debug,
                            cancellationToken: cancellationToken);
                        if (!result.Success)
                        {
                            throw new Exception("Internal error");
                        }
                    }
                }
            }
        }

        public static Task<EmitResult> SaveCompilationAsync(this CompositionConfiguration configuration, string assemblyName, Stream assemblyStream, Stream pdbStream = null, Stream sourceFile = null, TextWriter buildOutput = null, bool debug = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, "configuration");
            Requires.NotNullOrEmpty(assemblyName, "assemblyName");
            Requires.NotNull(assemblyStream, "assemblyStream");

            return Task.Run(async delegate
            {
                cancellationToken.ThrowIfCancellationRequested();

                CSharpCompilation templateCompilation = CreateTemplateCompilation(assemblyName, debug);

                var compilation = await AddGeneratedCodeAndDependenciesAsync(templateCompilation, configuration, sourceFile, debug, cancellationToken);

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
                Assembly.Load("System.Collections, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                typeof(ILazy<>).Assembly,
                typeof(Lazy<,>).Assembly,
                typeof(Enumerable).Assembly,
                typeof(System.Composition.ExportFactory<>).Assembly,
                typeof(ImmutableDictionary).Assembly);

            var diagnosticOptions = ImmutableDictionary.Create<string, ReportDiagnostic>()
                .Add("CS1701", ReportDiagnostic.Suppress)  // this is unavoidable. Roslyn doesn't let us supply runtime policy.
                .Add("CS0618", ReportDiagnostic.Suppress)  // calling obsolete code in generated code is how we roll.
                .Add("CS0162", ReportDiagnostic.Error);    // dead code emitted can be a sign of defects.

            return CSharpCompilation.Create(
                assemblyName,
                references: referenceAssemblies.Select(a => MetadataFileReferenceProvider.Default.GetReference(a.Location, MetadataReferenceProperties.Assembly)),
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimize: !debug,
                    debugInformationKind: debug ? DebugInformationKind.Full : DebugInformationKind.None,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                    specificDiagnosticOptions: diagnosticOptions));
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
                await buildOutput.WriteLineAsync("Generated assembly size: " + assemblyStream.Length);
                var compositionAssembly = Assembly.Load(assemblyStream.ToArray());
                return Load(compositionAssembly);
            }
            else
            {
                throw new Exception("Internal error.");
            }
        }

        public static IExportProviderFactory Load(AssemblyName assemblyRef)
        {
            return new CompiledExportProviderFactory(Assembly.Load(assemblyRef));
        }

        public static IExportProviderFactory Load(Assembly assembly)
        {
            return new CompiledExportProviderFactory(assembly);
        }

        private static async Task<CSharpCompilation> AddGeneratedCodeAndDependenciesAsync(CSharpCompilation compilationTemplate, CompositionConfiguration configuration, Stream sourceFile, bool debug, CancellationToken cancellationToken = default(CancellationToken))
        {
            var templateFactory = new SyntaxCodeGeneration();
            templateFactory.Configuration = configuration;
            var source = templateFactory.CreateSourceFile();

            SyntaxTree syntaxTree = source.SyntaxTree;
            if (sourceFile != null)
            {
                source = source.NormalizeWhitespace();
                FileStream sourceFileStream = sourceFile as FileStream;
                string sourceFilePath = sourceFileStream != null ? sourceFileStream.Name : null;
                Encoding encoding = new UTF8Encoding(true);
                var writer = new StreamWriter(sourceFile, encoding);
                source.WriteTo(writer);
                await writer.FlushAsync();

                // Unfortunately, we have to reparse the file in order to get the encoding into Roslyn
                // so its compiler doesn't fail with a CS8055 error.
                sourceFile.Position = 0;
                syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(sourceFile, encoding), path: sourceFilePath ?? string.Empty, cancellationToken: cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var assemblies = ImmutableHashSet.Create<Assembly>()
                .Union(configuration.AdditionalReferenceAssemblies)
                .Union(templateFactory.RelevantAssemblies);
            var embeddedInteropAssemblies = CreateEmbeddedInteropAssemblies(templateFactory.RelevantEmbeddedTypes, assemblies, debug);

            // We don't actually embed interop types on referenced assemblies because if we do, some of our tests fails due to
            // the CLR's inability to type cast Lazy<NoPIA> objects across assembly boundaries.
            var embedInteropTypesOptions = MetadataReferenceProperties.Assembly; // new MetadataReferenceProperties(embedInteropTypes: true);

            return compilationTemplate
                .AddReferences(assemblies.Select(r =>
                    MetadataFileReferenceProvider.Default.GetReference(
                        r.Location,
                        r.IsEmbeddableAssembly() ? embedInteropTypesOptions : MetadataReferenceProperties.Assembly)))
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
            var ifaceType = iface.GetCustomAttributesCached<System.Runtime.InteropServices.InterfaceTypeAttribute>().FirstOrDefault();
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

        private static IEnumerable<CSharpCompilation> CreateEmbeddedInteropAssemblies(IEnumerable<Type> embeddedTypes, IEnumerable<Assembly> referencedAssemblies, bool debug)
        {
            Requires.NotNull(embeddedTypes, "embeddedTypes");
            Requires.NotNull(referencedAssemblies, "referencedAssemblies");

            // Collect a set of all embeddable types from referenced assemblies.
            var referencedEmbeddableTypes = new HashSet<string>(from assembly in referencedAssemblies
                                                                where assembly.IsEmbeddableAssembly()
                                                                from type in assembly.GetExportedTypes()
                                                                where !type.GetCustomAttributesCached<TypeIdentifierAttribute>().Any() // embedded types are not embeddable -- we'll have to synthesize them ourselves
                                                                select type.FullName);

            embeddedTypes = embeddedTypes.Distinct(EquivalentTypesComparer.Instance)
                .Where(t => !referencedEmbeddableTypes.Contains(t.FullName));

            if (!embeddedTypes.Any())
            {
                return Enumerable.Empty<CSharpCompilation>();
            }

            var sourceFile = CreateTemplateEmbeddableTypesFile()
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(embeddedTypes.Select(iface => DefineEmbeddableType(iface))));

            var assemblies = ImmutableHashSet.Create<Assembly>(
                typeof(Guid).Assembly,
                typeof(string).Assembly);

            var compilationUnit = CSharpCompilation.Create("NoPIA")
                .AddSyntaxTrees(sourceFile.SyntaxTree)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(assemblies.Select(r => MetadataFileReferenceProvider.Default.GetReference(r.Location, MetadataReferenceProperties.Assembly)));

            // We don't actually need to emit the assembly. We only do that if we think diagnostics will come out of a failed build and we need to know why.
            // Perf wise, Emit is expensive even for something small like this, so we skip it when we can.
            if (debug)
            {
                var result = compilationUnit.Emit(Stream.Null);
                if (!result.Success)
                {
                    throw new CompositionFailedException("Failed to generate embeddable types.");
                }
            }

            return ImmutableList.Create(compilationUnit);
        }

        private class EquivalentTypesComparer : IEqualityComparer<Type>
        {
            private EquivalentTypesComparer() { }

            internal static readonly EquivalentTypesComparer Instance = new EquivalentTypesComparer();

            public bool Equals(Type x, Type y)
            {
                return x.IsEquivalentTo(y);
            }

            public int GetHashCode(Type obj)
            {
                return obj.FullName.GetHashCode();
            }
        }

        private class CompiledExportProviderFactory : IExportProviderFactory
        {
            private Func<ExportProvider> createFactory;

            internal CompiledExportProviderFactory(Assembly assembly)
            {
                Requires.NotNull(assembly, "assembly");

                var exportFactoryType = assembly.GetType("CompiledExportProvider");
                this.createFactory = () => (ExportProvider)Activator.CreateInstance(exportFactoryType);
            }

            public ExportProvider CreateExportProvider()
            {
                return this.createFactory();
            }
        }
    }
}
