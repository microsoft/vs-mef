namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.Utilities;
    using Validation;
    using Task = System.Threading.Tasks.Task;

    public class CompositionConfiguration
    {
        private CompositionConfiguration(ComposableCatalog catalog, ISet<ComposablePart> parts)
        {
            Requires.NotNull(catalog, "catalog");
            Requires.NotNull(parts, "parts");

            this.Catalog = catalog;
            this.Parts = parts;
        }

        public ComposableCatalog Catalog { get; private set; }

        public ISet<ComposablePart> Parts { get; private set; }

        public static CompositionConfiguration Create(ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            // Construct up our part builders, initialized with all their imports satisfied.
            var partBuilders = new Dictionary<ComposablePartDefinition, PartBuilder>();
            foreach (ComposablePartDefinition partDefinition in catalog.Parts)
            {
                var imports = partDefinition.ImportingMembers.Select(i => new Import(partDefinition, i.Value, i.Key));
                if (partDefinition.ImportingConstructor != null)
                {
                    imports = imports.Concat(partDefinition.ImportingConstructor.Select(i => new Import(partDefinition, i)));
                }

                var satisfyingImports = imports.ToImmutableDictionary(i => i, i => catalog.GetExports(i.ImportDefinition));
                partBuilders.Add(partDefinition, new PartBuilder(partDefinition, satisfyingImports));
            }

            // Create a lookup table that gets all immediate importers for each part.
            foreach (PartBuilder partBuilder in partBuilders.Values)
            {
                var importedPartsExcludingFactories =
                    (from entry in partBuilder.SatisfyingExports
                     where !entry.Key.ImportDefinition.IsExportFactory
                     from export in entry.Value
                     select export.PartDefinition).Distinct();
                foreach (var importedPartDefinition in importedPartsExcludingFactories)
                {
                    var importedPartBuilder = partBuilders[importedPartDefinition];
                    importedPartBuilder.ReportImportingPart(partBuilder);
                }
            }

            // Propagate sharing boundaries defined on parts to all importers (transitive closure).
            foreach (PartBuilder partBuilder in partBuilders.Values)
            {
                partBuilder.ApplySharingBoundary();
            }

            // Build up our set of composed parts.
            var partsBuilder = ImmutableHashSet.CreateBuilder<ComposablePart>();
            foreach (var partBuilder in partBuilders.Values)
            {
                var composedPart = new ComposablePart(partBuilder.PartDefinition, partBuilder.SatisfyingExports, partBuilder.EffectiveSharingBoundaries.ToImmutableHashSet());
                partsBuilder.Add(composedPart);
            }

            var parts = partsBuilder.ToImmutable();

            // Validate configuration.
            foreach (var part in parts)
            {
                var exceptions = new List<Exception>();
                try
                {
                    part.Validate();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }
            }

            // Detect loops of all non-shared parts.
            if (IsLoopPresent(parts))
            {
                Console.WriteLine(CreateDgml(parts));
                Verify.FailOperation("Loop detected.");
            }

            return new CompositionConfiguration(catalog, parts);
        }

        public static CompositionConfiguration Create(params Type[] parts)
        {
            Requires.NotNull(parts, "parts");

            return Create(ComposableCatalog.Create(parts));
        }

        public static ICompositionContainerFactory LoadDefault()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName.Replace(".vshost", string.Empty);
            string baseName = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath));
            string defaultCompositionFile = baseName + ".Composition.dll";
            return Load(defaultCompositionFile);
        }

        public static ICompositionContainerFactory Load(string path)
        {
            return new ContainerFactory(Assembly.LoadFile(path));
        }

        public async Task SaveAsync(string assemblyPath)
        {
            Requires.NotNullOrEmpty(assemblyPath, "assemblyPath");

            var sourceFilePathAndAssemblies = this.CreateCompositionSourceFile();
            await this.CompileAsync(sourceFilePathAndAssemblies.Item1, sourceFilePathAndAssemblies.Item2, assemblyPath);
        }

        public async Task<ICompositionContainerFactory> CreateContainerFactoryAsync()
        {
            string targetPath = Path.GetTempFileName();
            await this.SaveAsync(targetPath);
            return Load(targetPath);
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

        private static bool IsLoopPresent(ImmutableHashSet<ComposablePart> parts)
        {
            var partsAndDirectImports = new Dictionary<ComposablePart, ImmutableHashSet<ComposablePart>>();

            // First create a map of each NonShared part and the NonShared parts it directly imports.
            foreach (var part in parts.Where(p => !p.Definition.IsShared))
            {
                var directlyImportedParts = (from exportList in part.SatisfyingExports.Values
                                             from export in exportList
                                             let exportingPart = parts.Single(p => p.Definition == export.PartDefinition)
                                             where !exportingPart.Definition.IsShared
                                             select exportingPart).ToImmutableHashSet();
                partsAndDirectImports.Add(part, directlyImportedParts);
            }

            // Now create a map of each part and all the parts it transitively imports.
            return IsLoopPresent(partsAndDirectImports.Keys, p => partsAndDirectImports[p]);
        }

        private static bool IsLoopPresent<T>(IEnumerable<T> values, Func<T, IEnumerable<T>> getDirectLinks)
        {
            Requires.NotNull(values, "values");
            Requires.NotNull(getDirectLinks, "getDirectLinks");

            var visitedNodes = new HashSet<T>();
            var queue = new Queue<T>();
            foreach (T value in values)
            {
                visitedNodes.Clear();
                queue.Clear();

                queue.Enqueue(value);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    if (!visitedNodes.Add(node))
                    {
                        // Only claim to have detected a loop if we got back to the *original* part.
                        // This is because they may be multiple legit routes from the original part
                        // to the part we're looking at now.
                        if (value.Equals(node))
                        {
                            return true;
                        }
                    }

                    foreach (var directLink in getDirectLinks(node).Distinct())
                    {
                        queue.Enqueue(directLink);
                    }
                }
            }

            return false;
        }

        private Tuple<string, ISet<Assembly>> CreateCompositionSourceFile()
        {
            var templateFactory = new CompositionTemplateFactory();
            templateFactory.Configuration = this;
            string source = templateFactory.TransformText();
            var sourceFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cs");
            File.WriteAllText(sourceFilePath, source);
            WriteWithLineNumbers(Console.Out, source);
            return Tuple.Create(sourceFilePath, templateFactory.RelevantAssemblies);
        }

        private async Task CompileAsync(string sourceFilePath, ISet<Assembly> assemblies, string targetPath)
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

        public XDocument CreateDgml()
        {
            return CreateDgml(this.Parts);
        }

        private static XDocument CreateDgml(ISet<ComposablePart> parts)
        {
            Requires.NotNull(parts, "parts");

            XElement nodes, links;
            var dgml = Dgml.Create(out nodes, out links);

            foreach (var part in parts)
            {
                nodes.Add(Dgml.Node(part.Definition.Id, part.Definition.Id));
                foreach (var import in part.SatisfyingExports.Keys)
                {
                    foreach (Export export in part.SatisfyingExports[import])
                    {
                        links.Add(Dgml.Link(export.PartDefinition.Id, part.Definition.Id));
                    }
                }
            }

            return dgml;
        }

        private class ContainerFactory : ICompositionContainerFactory
        {
            private Func<ExportProvider> createFactory;

            internal ContainerFactory(Assembly assembly)
            {
                Requires.NotNull(assembly, "assembly");

                var exportFactoryType = assembly.GetType("CompiledExportProvider");
                this.createFactory = () => (ExportProvider)Activator.CreateInstance(exportFactoryType);
            }

            public CompositionContainer CreateContainer()
            {
                return new CompositionContainer(this.createFactory());
            }
        }

        [DebuggerDisplay("{PartDefinition.Type.Name}")]
        private class PartBuilder
        {
            internal PartBuilder(ComposablePartDefinition partDefinition, IReadOnlyDictionary<Import, IReadOnlyList<Export>> importedParts)
            {
                Requires.NotNull(partDefinition, "partDefinition");
                Requires.NotNull(importedParts, "importedParts");

                this.PartDefinition = partDefinition;
                this.EffectiveSharingBoundaries = ImmutableHashSet.CreateBuilder<string>();
                this.SatisfyingExports = importedParts;
                this.ImportingParts = new HashSet<PartBuilder>();
            }

            /// <summary>
            /// Gets the part definition tracked by this instance.
            /// </summary>
            public ComposablePartDefinition PartDefinition { get; private set; }

            /// <summary>
            /// Gets the sharing boundaries applied to this part.
            /// </summary>
            public ISet<string> EffectiveSharingBoundaries { get; private set; }

            /// <summary>
            /// Gets the set of parts that import this one.
            /// </summary>
            public HashSet<PartBuilder> ImportingParts { get; private set; }

            /// <summary>
            /// Gets the set of parts imported by this one.
            /// </summary>
            public IReadOnlyDictionary<Import, IReadOnlyList<Export>> SatisfyingExports { get; private set; }

            public void ApplySharingBoundary()
            {
                this.ApplySharingBoundary(this.PartDefinition.SharingBoundary);
            }

            private void ApplySharingBoundary(string sharingBoundary)
            {
                if (!string.IsNullOrEmpty(sharingBoundary))
                {
                    if (this.EffectiveSharingBoundaries.Add(sharingBoundary))
                    {
                        // Since this is new to us, be sure that all our importers belong to this sharing boundary as well.
                        foreach (var importingPart in this.ImportingParts)
                        {
                            importingPart.ApplySharingBoundary(sharingBoundary);
                        }
                    }
                }
            }

            public void ReportImportingPart(PartBuilder part)
            {
                this.ImportingParts.Add(part);
            }
        }
    }
}
