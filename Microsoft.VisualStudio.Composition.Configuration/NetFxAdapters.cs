namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
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
                // MEFv1 deals exclusively with strings at this level: a particular string representation
                // of the exported type name, and the contract name. 
                // It seems the contract name is the first test for matching, followed by 
                // either execution or interpretation of the ImportDefinition constraint which has
                // the type name encoded into it.
                // TODO: We need to make such a lookup possible in MEFv3 as well in order to allow for
                //       shimming into V1 like this.
                var v3Exports = this.exportProvider.GetExports(typeof(object), definition.ContractName);
                return v3Exports.Select(e => new MefV1.Primitives.Export(definition.ContractName, () => e.Value));
            }
        }
    }
}
