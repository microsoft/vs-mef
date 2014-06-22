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
                var v3ImportDefinition = WrapImportDefinition(definition);
                var result = ImmutableList.CreateBuilder<MefV1.Primitives.Export>();
                var exports = this.exportProvider.GetExports(v3ImportDefinition);
                return exports.Select(e => new MefV1.Primitives.Export(e.Definition.Contract.ContractName, (IDictionary<string, object>)e.Metadata, () => e.Value));
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
                    exportDefinition.Contract.ContractName,
                    (IDictionary<string, object>)exportDefinition.Metadata);
                return this.definition.IsConstraintSatisfiedBy(v3ExportDefinition);
            }
        }
    }
}
