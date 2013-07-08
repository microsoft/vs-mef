namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.Utilities;
    using Validation;
    using Task = System.Threading.Tasks.Task;

    public class CompositionConfiguration : ICompositionContainerFactory
    {
        private Lazy<Assembly> precompiledAssembly;
        private Lazy<ContainerFactory> containerFactory;

        private CompositionConfiguration(ComposableCatalog catalog, ISet<ComposablePart> parts)
        {
            Requires.NotNull(catalog, "catalog");
            Requires.NotNull(parts, "parts");

            this.Catalog = catalog;
            this.Parts = parts;

            // Arrange for actually compiling the assembly when asked for.
            this.precompiledAssembly = new Lazy<Assembly>(this.CreateAssembly, true);
            this.containerFactory = new Lazy<ContainerFactory>(() => new ContainerFactory(this.precompiledAssembly.Value), true);
        }

        public ComposableCatalog Catalog { get; private set; }

        public ISet<ComposablePart> Parts { get; private set; }

        public static CompositionConfiguration Create(ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            var partsBuilder = ImmutableHashSet.CreateBuilder<ComposablePart>();

            foreach (ComposablePartDefinition part in catalog.Parts)
            {
                var satisfyingImports = part.ImportDefinitions.ToImmutableDictionary(
                    i => new Import(part, i.Value, i.Key),
                    i => catalog.GetExports(i.Value));
                var composedPart = new ComposablePart(part, satisfyingImports);
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
            CheckForLoopsOfNonSharedParts(parts);

            return new CompositionConfiguration(catalog, parts);
        }

        public static CompositionConfiguration Create(params Type[] parts)
        {
            Requires.NotNull(parts, "parts");

            return Create(ComposableCatalog.Create(parts));
        }

        public static ICompositionContainerFactory Load(string path)
        {
            return new ContainerFactory(Assembly.LoadFile(path));
        }

        public void Save(string assemblyPath)
        {
            Requires.NotNullOrEmpty(assemblyPath, "assemblyPath");

            File.Copy(precompiledAssembly.Value.Location, assemblyPath);
        }

        public CompositionContainer CreateContainer()
        {
            return this.containerFactory.Value.CreateContainer();
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

        private static void CheckForLoopsOfNonSharedParts(ImmutableHashSet<ComposablePart> parts)
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
            Verify.Operation(!IsLoopPresent(partsAndDirectImports.Keys, p => partsAndDirectImports[p]), "Loop detected.");
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
                        return true;
                    }

                    foreach (var directLink in getDirectLinks(node).Distinct())
                    {
                        queue.Enqueue(directLink);
                    }
                }
            }

            return false;
        }

        private string CreateCompositionSourceFile()
        {
            var templateFactory = new CompositionTemplateFactory();
            templateFactory.Configuration = this;
            string source = templateFactory.TransformText();
            var sourceFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cs");
            File.WriteAllText(sourceFilePath, source);
            WriteWithLineNumbers(Console.Out, source);
            return sourceFilePath;
        }

        private Assembly CreateAssembly()
        {
            var sourceFilePath = this.CreateCompositionSourceFile();
            Assembly precompiledComposition = this.Compile(sourceFilePath);
            return precompiledComposition;
        }

        private Assembly Compile(string sourceFilePath)
        {
            var targetPath = Path.GetTempFileName();
            var provider = CodeDomProvider.CreateProvider("c#");
            var parameters = new CompilerParameters(new[] { typeof(Enumerable).Assembly.Location, Assembly.GetExecutingAssembly().Location });
            parameters.IncludeDebugInformation = true;
            parameters.ReferencedAssemblies.AddRange(this.Catalog.Assemblies.Select(a => a.Location).Distinct().ToArray());
            parameters.ReferencedAssemblies.Add(typeof(System.ComponentModel.Composition.IPartImportsSatisfiedNotification).Assembly.Location);
            //parameters.ReferencedAssemblies.Add(typeof(System.Collections.Immutable.ImmutableDictionary).Assembly.Location);
            //parameters.CompilerOptions += @" /r:sr=""C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5\System.Runtime.dll""";
            // TODO: we must reference all assemblies that define the types we touch, or that define types implemented by the types we touch.
            parameters.OutputAssembly = targetPath;
            CompilerResults results = provider.CompileAssemblyFromFile(parameters, sourceFilePath);
            if (results.Errors.HasErrors || results.Errors.HasWarnings)
            {
                foreach (var error in results.Errors)
                {
                    Console.WriteLine(error);
                }
            }

            Verify.Operation(!results.Errors.HasErrors, "Compilation errors occurred.");
            return results.CompiledAssembly;
        }

        public XDocument CreateDgml()
        {
            XElement nodes, links;
            var dgml = Dgml.Create(out nodes, out links);

            foreach (var part in this.Parts)
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
    }
}
