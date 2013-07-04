namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ComposablePart
    {
        public ComposablePart(ComposablePartDefinition definition, IReadOnlyDictionary<ImportDefinition, IReadOnlyList<Export>> satisfyingExports)
        {
            Requires.NotNull(definition, "definition");
            Requires.NotNull(satisfyingExports, "satisfyingExports");

            // Make sure we have entries for every import.
            Requires.Argument(satisfyingExports.Count == definition.ImportDefinitions.Count && definition.ImportDefinitions.All(d => satisfyingExports.ContainsKey(d.Value)), "satisfyingExports", "There should be exactly one entry for every import.");
            Requires.Argument(satisfyingExports.All(kv => kv.Value != null), "satisfyingExports", "All values must be non-null.");

            this.Definition = definition;
            this.SatisfyingExports = satisfyingExports;
        }

        public ComposablePartDefinition Definition { get; private set; }

        public IReadOnlyDictionary<ImportDefinition, IReadOnlyList<Export>> SatisfyingExports { get; private set; }

        public void Validate()
        {
            foreach (var pair in this.SatisfyingExports)
            {
                switch (pair.Key.Cardinality)
                {
                    case ImportCardinality.ExactlyOne:
                        Verify.Operation(pair.Value.Count == 1, "Import of {0} expected 1 export but found {1}.", pair.Key.Contract, pair.Value.Count);
                        break;
                    case ImportCardinality.OneOrZero:
                        Verify.Operation(pair.Value.Count < 2, "Import of {0} expected 1 or 0 exports but found {1}.", pair.Key.Contract, pair.Value.Count);
                        break;
                }
            }
        }
    }
}
