namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.Utilities;
    using Validation;
    using Task = System.Threading.Tasks.Task;

    public class CompositionConfiguration
    {
        internal CompositionConfiguration(IReadOnlyCollection<ComposablePart> parts)
        {
            Requires.NotNull(parts, "parts");

            this.Parts = parts;
        }

        public IReadOnlyCollection<ComposablePart> Parts { get; private set; }

        public IReadOnlyCollection<ComposablePart> GetPartsWithExportsSatisfying(ImportDefinition importDefinition)
        {
            return (from part in this.Parts
                    from export in part.ExportDefinitions
                    where export.Contract.Equals(importDefinition.Contract)
                    select part).Distinct().ToList();
        }

        public Task<ContainerFactory> CreateContainerFactoryAsync()
        {
            var sourceFilePath = CreateCompositionSourceFile();
            Assembly precompiledComposition = Compile(sourceFilePath);
            return Task.FromResult(new ContainerFactory(precompiledComposition));
        }

        private string CreateCompositionSourceFile()
        {
            var templateFactory = new CompositionTemplateFactory();
            templateFactory.Configuration = this;
            string source = templateFactory.TransformText();
            var sourceFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cs");
            File.WriteAllText(sourceFilePath, source);
            Console.WriteLine(source);
            return sourceFilePath;
        }

        private Assembly Compile(string sourceFilePath)
        {
            var targetPath = Path.GetTempFileName();
            var provider = CodeDomProvider.CreateProvider("c#");
            var parameters = new CompilerParameters(new[] { typeof(Enumerable).Assembly.Location, Assembly.GetExecutingAssembly().Location });
            parameters.IncludeDebugInformation = true;
            parameters.ReferencedAssemblies.AddRange(this.Parts.Select(p => p.Type.Assembly.Location).Distinct().ToArray());
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
    }
}
