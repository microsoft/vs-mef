namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
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

        public static async Task SaveAsync(this CompositionConfiguration configuration, string assemblyPath, string pdbPath = null, string sourceFilePath = null, CancellationToken cancellationToken = default(CancellationToken))
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
                            if (diagnostic.Location != Location.None)
                            {
                                await buildOutput.WriteAsync("Line " + (diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1) + ": ");
                            }

                            await buildOutput.WriteLineAsync(diagnostic.Category + " " + diagnostic.Severity + " " + diagnostic.Id + ": " + diagnostic.GetMessage());
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
                typeof(ILazy<>).Assembly,
                typeof(System.Composition.ExportFactory<>).Assembly,
                typeof(ImmutableDictionary).Assembly);

            return CSharpCompilation.Create(
                assemblyName,
                references: referenceAssemblies.Select(a => MetadataReferenceProvider.Default.GetReference(a.Location)),
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimize: !debug,
                    debugInformationKind: debug ? DebugInformationKind.Full : DebugInformationKind.None));
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
                syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(sourceFile, encoding), sourceFilePath ?? string.Empty, cancellationToken: cancellationToken);
            }
            else
            {
                syntaxTree = SyntaxFactory.ParseSyntaxTree(source);
            }

            var assemblies = ImmutableHashSet.Create<Assembly>()
                .Union(configuration.AdditionalReferenceAssemblies)
                .Union(templateFactory.RelevantAssemblies);
            return compilationTemplate
                .AddReferences(assemblies.Select(r => MetadataReferenceProvider.Default.GetReference(r.Location)))
                .AddSyntaxTrees(syntaxTree);
        }
    }
}
