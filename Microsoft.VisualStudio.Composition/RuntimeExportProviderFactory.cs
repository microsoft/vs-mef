namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    internal partial class RuntimeExportProviderFactory : IExportProviderFactory
    {
        private readonly RuntimeComposition composition;

        internal RuntimeExportProviderFactory(RuntimeComposition composition)
        {
            Requires.NotNull(composition, "composition");
            this.composition = composition;
        }

        public ExportProvider CreateExportProvider()
        {
            return new RuntimeExportProvider(this.composition);
        }
    }
}
