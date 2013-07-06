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

            var parts = ImmutableHashSet.CreateBuilder<ComposablePart>();

            foreach (ComposablePartDefinition part in catalog.Parts)
            {
                var satisfyingImports = part.ImportDefinitions.ToImmutableDictionary(
                    i => new Import(part, i.Value, i.Key),
                    i => catalog.GetExports(i.Value));
                var composedPart = new ComposablePart(part, satisfyingImports);
                parts.Add(composedPart);
            }

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

            return new CompositionConfiguration(catalog, parts.ToImmutable());
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
            private Func<ExportFactory> createFactory;

            internal ContainerFactory(Assembly assembly)
            {
                Requires.NotNull(assembly, "assembly");

                var exportFactoryType = assembly.GetType("CompiledExportFactory");
                this.createFactory = () => (ExportFactory)Activator.CreateInstance(exportFactoryType);
            }

            public CompositionContainer CreateContainer()
            {
                return new CompositionContainer(this.createFactory());
            }
        }
    }
}
