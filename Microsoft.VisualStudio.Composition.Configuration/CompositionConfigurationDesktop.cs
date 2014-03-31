namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging;
    using Validation;

    public static class CompositionConfigurationDesktop
    {
        public static ICompositionContainerFactory LoadDefault()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName.Replace(".vshost", string.Empty);
            string baseName = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath));
            string defaultCompositionFile = baseName + ".Composition.dll";
            return CompositionConfiguration.Load(Assembly.LoadFile(defaultCompositionFile));
        }

        public static async Task SaveAsync(this CompositionConfiguration configuration, string assemblyPath)
        {
            Requires.NotNullOrEmpty(assemblyPath, "assemblyPath");

            var sourceFilePathAndAssemblies = CreateCompositionSourceFile(configuration);
            await CompileAsync(sourceFilePathAndAssemblies.Item1, sourceFilePathAndAssemblies.Item2, assemblyPath);
        }

        public static async Task<ICompositionContainerFactory> CreateContainerFactoryAsync(this CompositionConfiguration configuration)
        {
            string targetPath = Path.GetTempFileName();
            await configuration.SaveAsync(targetPath);
            return CompositionConfiguration.Load(Assembly.LoadFile(targetPath));
        }

        private static void WriteWithLineNumbers(TextWriter writer, string content)
        {
            Requires.NotNull(writer, "writer");
            Requires.NotNull(content, "content");

            int lineNumber = 0;
            foreach (string line in content.Split('\n'))
            {
                writer.WriteLine("{0,5}: {1}", ++lineNumber, line.Trim('\r', '\n'));
            }
        }

        private static Tuple<string, ISet<Assembly>> CreateCompositionSourceFile(CompositionConfiguration configuration)
        {
            var templateFactory = new CompositionTemplateFactory();
            templateFactory.Configuration = configuration;
            string source = templateFactory.TransformText();
            var sourceFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cs");
            File.WriteAllText(sourceFilePath, source);
            WriteWithLineNumbers(Console.Out, source);
            return Tuple.Create(sourceFilePath, templateFactory.RelevantAssemblies);
        }

        private static async Task CompileAsync(string sourceFilePath, ISet<Assembly> assemblies, string targetPath)
        {
            targetPath = Path.GetFullPath(targetPath);
            var pc = new ProjectCollection();
            ProjectRootElement pre;
            using (var templateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.VisualStudio.Composition.CompositionTemplateFactory.csproj"))
            {
                using (var xmlReader = XmlReader.Create(templateStream))
                {
                    pre = ProjectRootElement.Create(xmlReader, pc);
                }
            }

            var globalProperties = new Dictionary<string, string> {
                { "Configuration", "Debug" },
            };
            var project = new Project(pre, globalProperties, null, pc);
            project.SetProperty("AssemblyName", Path.GetFileNameWithoutExtension(targetPath));
            project.AddItem("Compile", ProjectCollection.Escape(sourceFilePath));
            project.AddItem("Reference", ProjectCollection.Escape(Assembly.GetExecutingAssembly().Location));
            project.AddItem("Reference", ProjectCollection.Escape(typeof(ILazy<>).Assembly.Location));
            project.AddItem("Reference", ProjectCollection.Escape(typeof(System.Composition.ExportFactory<>).Assembly.Location));
            project.AddItem("Reference", ProjectCollection.Escape(typeof(System.Collections.Immutable.ImmutableDictionary).Assembly.Location));
            foreach (var assembly in assemblies)
            {
                project.AddItem("Reference", ProjectCollection.Escape(assembly.Location));
            }

            string projectPath = Path.GetTempFileName();
            project.Save(projectPath);
            BuildResult buildResult;
            using (var buildManager = new BuildManager())
            {
                var hostServices = new HostServices();
                var logger = new ConsoleLogger(LoggerVerbosity.Minimal);
                buildManager.BeginBuild(new BuildParameters(pc)
                {
                    DisableInProcNode = true,
                    Loggers = new ILogger[] { logger },
                });
                var buildSubmission = buildManager.PendBuildRequest(new BuildRequestData(projectPath, globalProperties, null, new[] { "Build", "GetTargetPath" }, hostServices));
                buildResult = await buildSubmission.ExecuteAsync();
                buildManager.EndBuild();
            }

            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                Console.WriteLine("Build errors");
            }

            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                throw new InvalidOperationException("Build failed. Project File was \"" + projectPath + "\"", buildResult.Exception);
            }

            string finalAssemblyPath = buildResult.ResultsByTarget["GetTargetPath"].Items.Single().ItemSpec;
            if (!string.Equals(finalAssemblyPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                // If the caller requested a non .dll extension, we need to honor that.
                File.Delete(targetPath);
                File.Move(finalAssemblyPath, targetPath);
            }
        }
    }
}
