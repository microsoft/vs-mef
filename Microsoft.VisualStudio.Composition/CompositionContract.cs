namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    [DebuggerDisplay("{Type.Name,nq} ({ContractName})")]
    public class CompositionContract : IEquatable<CompositionContract>
    {
        public CompositionContract(string contractName, Type type)
        {
            Requires.NotNull(type, "type");

            this.ContractName = contractName; // ?? PartDiscovery.GetContractName(type);
            this.Type = type;
        }

        public string ContractName { get; private set; }

        public Type Type { get; private set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as CompositionContract);
        }

        public bool Equals(CompositionContract other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Type.IsEquivalentTo(other.Type)
                && StringComparer.Ordinal.Equals(this.ContractName, other.ContractName);
        }

        public override int GetHashCode()
        {
            // Use the type's full name for its hash code rather than
            // calling Type.GetHashCode() directly. This allows contracts to
            // equivalent embeddedable types to generate the same hash code.
            return this.Type.FullName.GetHashCode() + (this.ContractName != null ? StringComparer.Ordinal.GetHashCode(this.ContractName) : 0);
        }

        public override string ToString()
        {
            string contractSuffix = this.ContractName != null
                ? " (" + this.ContractName + ")"
                : string.Empty;
            return ReflectionHelpers.GetTypeName(this.Type, false, true, null) + contractSuffix;
        }
    }
}
