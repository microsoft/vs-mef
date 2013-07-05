namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ImportDefinition  : IEquatable<ImportDefinition>
    {
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality)
        {
            Requires.NotNull(contract, "contract");

            this.Contract = contract;
            this.Cardinality = cardinality;
        }

        public ImportCardinality Cardinality { get; private set; }

        public bool IsLazy { get; private set; }

        public CompositionContract Contract { get; private set; }

        public override int GetHashCode()
        {
            return this.Contract.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ImportDefinition);
        }

        public bool Equals(ImportDefinition other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Contract.Equals(other.Contract)
                && this.Cardinality == other.Cardinality;
        }
    }
}
