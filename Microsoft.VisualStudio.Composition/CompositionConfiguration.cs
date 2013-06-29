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
        private readonly IReadOnlyList<Type> parts;

        internal CompositionConfiguration(IReadOnlyList<Type> parts)
        {
            Requires.NotNull(parts, "parts");

            this.parts = parts;
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
            templateFactory.Parts = this.parts;
            string source = templateFactory.TransformText();
            var sourceFilePath = Path.GetTempFileName();
            File.WriteAllText(sourceFilePath, source);
            return sourceFilePath;
        }

        private Assembly Compile(string sourceFilePath)
        {
            var targetPath = Path.GetTempFileName();
            var provider = CodeDomProvider.CreateProvider("c#");
            var parameters = new CompilerParameters(new[] { typeof(Enumerable).Assembly.Location, Assembly.GetExecutingAssembly().Location });
            parameters.IncludeDebugInformation = true;
            parameters.ReferencedAssemblies.AddRange(this.parts.Select(p => p.Assembly.Location).Distinct().ToArray());
            parameters.OutputAssembly = targetPath;
            CompilerResults results = provider.CompileAssemblyFromFile(parameters, sourceFilePath);
            Verify.Operation(!results.Errors.HasErrors, "Compilation errors occurred.");
            return results.CompiledAssembly;
        }
    }
}
