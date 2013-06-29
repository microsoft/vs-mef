namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ContainerFactory
    {
        private readonly Assembly compiledComposition;

        internal ContainerFactory(Assembly compiledComposition)
        {
            Requires.NotNull(compiledComposition, "compiledComposition");

            this.compiledComposition = compiledComposition;
        }

        public CompositionContainer CreateContainer()
        {
            var exportFactoryType = this.compiledComposition.GetType("CompiledExportFactory");
            var exportFactory = (ExportFactory)Activator.CreateInstance(exportFactoryType);
            return new CompositionContainer(exportFactory);
        }
    }
}
