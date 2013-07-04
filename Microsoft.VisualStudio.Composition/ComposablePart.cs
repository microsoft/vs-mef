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
    }
}
