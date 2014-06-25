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
        /// <summary>
        /// Creates an instance of a <see cref="MefV1.Hosting.ExportProvider"/>
        /// for purposes of compatibility with the version of MEF found in the .NET Framework.
        /// </summary>
        /// <param name="exportProvider">The <see cref="Microsoft.VisualStudio.Composition.ExportProvider"/> to wrap.</param>
        /// <returns>A MEF "v1" shim.</returns>
        public static MefV1.Hosting.ExportProvider AsExportProvider(this ExportProvider exportProvider)
        {
            Requires.NotNull(exportProvider, "exportProvider");

            return new MefV1ExportProvider(exportProvider);
        }

        /// <summary>
        /// Creates a catalog that exports an instance of <see cref="MefV1.ICompositionService"/>.
        /// </summary>
        /// <param name="catalog">The catalog to add the export to.</param>
        /// <returns>A catalog that includes <see cref="MefV1.ICompositionService"/>.</returns>
        public static ComposableCatalog WithCompositionService(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            var partDiscovery = new AttributedPartDiscoveryV1();
            var compositionServicePart = partDiscovery.CreatePart(typeof(CompositionService));
            var modifiedCatalog = catalog.WithPart(compositionServicePart);
            return modifiedCatalog;
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
                var v3ImportDefinition = WrapImportDefinition(definition);
                var result = ImmutableList.CreateBuilder<MefV1.Primitives.Export>();
                var exports = this.exportProvider.GetExports(v3ImportDefinition);
                return exports.Select(e => new MefV1.Primitives.Export(e.Definition.ContractName, (IDictionary<string, object>)e.Metadata, () => e.Value));
            }

            private static ImportDefinition WrapImportDefinition(MefV1.Primitives.ImportDefinition definition)
            {
                Requires.NotNull(definition, "definition");
                var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty.Add(new ImportConstraint(definition));
                var cardinality = WrapCardinality(definition.Cardinality);
                return new ImportDefinition(definition.ContractName, cardinality, (IReadOnlyDictionary<string, object>)definition.Metadata, constraints);
            }

            private static ImportCardinality WrapCardinality(MefV1.Primitives.ImportCardinality cardinality)
            {
                switch (cardinality)
                {
                    case System.ComponentModel.Composition.Primitives.ImportCardinality.ExactlyOne:
                        return ImportCardinality.ExactlyOne;
                    case System.ComponentModel.Composition.Primitives.ImportCardinality.ZeroOrMore:
                        return ImportCardinality.ZeroOrMore;
                    case System.ComponentModel.Composition.Primitives.ImportCardinality.ZeroOrOne:
                        return ImportCardinality.OneOrZero;
                    default:
                        throw new ArgumentException();
                }
            }
        }

        private class ImportConstraint : IImportSatisfiabilityConstraint
        {
            private readonly MefV1.Primitives.ImportDefinition definition;

            internal ImportConstraint(MefV1.Primitives.ImportDefinition definition)
            {
                Requires.NotNull(definition, "definition");
                this.definition = definition;
            }

            public bool IsSatisfiedBy(ExportDefinition exportDefinition)
            {
                var v3ExportDefinition = new MefV1.Primitives.ExportDefinition(
                    exportDefinition.ContractName,
                    (IDictionary<string, object>)exportDefinition.Metadata);
                return this.definition.IsConstraintSatisfiedBy(v3ExportDefinition);
            }
        }

        [MefV1.Export(typeof(MefV1.ICompositionService))]
        private class CompositionService : MefV1.ICompositionService, IDisposable
        {
            private MefV1.Hosting.CompositionContainer container;

            [MefV1.ImportingConstructor]
            private CompositionService([MefV1.Import] ExportProvider exportProvider)
            {
                Requires.NotNull(exportProvider, "exportProvider");
                this.container = new MefV1.Hosting.CompositionContainer(exportProvider.AsExportProvider());
            }

            public void SatisfyImportsOnce(MefV1.Primitives.ComposablePart part)
            {
                this.container.SatisfyImportsOnce(part);
            }

            public void Dispose()
            {
                this.container.Dispose();
            }
        }
    }
}
