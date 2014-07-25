namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal class RuntimeExportProviderFactory : IExportProviderFactory
    {
        private readonly CompositionConfiguration configuration;

        internal RuntimeExportProviderFactory(CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");
            this.configuration = configuration;
        }

        public ExportProvider CreateExportProvider()
        {
            return new RuntimeExportProvider(this.configuration);
        }

        private class RuntimeExportProvider : ExportProvider
        {
            private readonly CompositionConfiguration configuration;

            internal RuntimeExportProvider(CompositionConfiguration configuration)
                : this(configuration, null, null)
            {
            }

            internal RuntimeExportProvider(CompositionConfiguration configuration, ExportProvider parent, string[] freshSharingBoundaries)
                : base(parent, freshSharingBoundaries)
            {
                Requires.NotNull(configuration, "configuration");
                this.configuration = configuration;
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition importDefinition)
            {
                throw new NotImplementedException();
            }
        }
    }
}
