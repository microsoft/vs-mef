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

    [DebuggerDisplay("{Contract.Type.Name,nq}")]
    public class ExportDefinition : IEquatable<ExportDefinition>
    {
        public ExportDefinition(CompositionContract contract, IReadOnlyDictionary<string, object> metadata)
        {
            Requires.NotNull(contract, "contract");
            Requires.NotNull(metadata, "metadata");

            this.Contract = contract;
            this.Metadata = ImmutableDictionary.CreateRange(metadata)
                .SetItem(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(contract.Type));
        }

        public CompositionContract Contract { get; private set; }

        public IReadOnlyDictionary<string, object> Metadata { get; private set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExportDefinition);
        }

        public override int GetHashCode()
        {
            return this.Contract == null ? 0 : this.Contract.GetHashCode();
        }

        public bool Equals(ExportDefinition other)
        {
            return this.Contract.Equals(other.Contract);
        }
    }
}
