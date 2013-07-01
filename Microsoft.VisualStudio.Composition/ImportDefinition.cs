namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ImportDefinition
    {
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality)
        {
            Requires.NotNull(contract, "contract");

            this.Contract = contract;
            this.Cardinality = cardinality;
        }

        public ImportCardinality Cardinality { get; private set; }

        public CompositionContract Contract { get; private set; }
    }
}
