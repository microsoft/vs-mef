namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    [DebuggerDisplay("{ContractName,nq}")]
    public class ExportDefinition : IEquatable<ExportDefinition>
    {
        public ExportDefinition(string contractName, IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNullOrEmpty(contractName, "contractName");
            Requires.NotNull(metadata, "metadata");

            this.ContractName = contractName;
            this.Metadata = ImmutableDictionary.CreateRange(metadata);
        }

        public string ContractName { get; private set; }

        public IReadOnlyDictionary<string, object> Metadata { get; private set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExportDefinition);
        }

        public override int GetHashCode()
        {
            return this.ContractName.GetHashCode();
        }

        public bool Equals(ExportDefinition other)
        {
            return this.ContractName == other.ContractName
                && this.Metadata.EqualsByValue(other.Metadata);
        }
    }
}
