namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    public static class NetFxAdapters
    {
        public static MefV1.Hosting.ExportProvider AsExportProvider(this ExportProvider exportProvider)
        {
            Requires.NotNull(exportProvider, "exportProvider");

            return new MefV1ExportProvider(exportProvider);
        }

        private class MefV1ExportProvider : MefV1.Hosting.ExportProvider
        {
            private readonly ExportProvider exportProvider;

            internal MefV1ExportProvider(ExportProvider exportProvider)
            {
                Requires.NotNull(exportProvider, "exportProvider");

                this.exportProvider = exportProvider;
            }

            protected override IEnumerable<MefV1.Primitives.Export> GetExportsCore(MefV1.Primitives.ImportDefinition definition, MefV1.Hosting.AtomicComposition atomicComposition)
            {
                var result = ImmutableList.CreateBuilder<MefV1.Primitives.Export>();
                foreach (var export in this.exportProvider.GetExports(definition.ContractName))
                {
                    if (definition.IsConstraintSatisfiedBy(new MefV1.Primitives.ExportDefinition(definition.ContractName, export.Metadata)))
                    {
                        result.Add(new MefV1.Primitives.Export(definition.ContractName, () => export.Value));
                    }
                }

                return result.ToImmutable();
            }
        }
    }
}
