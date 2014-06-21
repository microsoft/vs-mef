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
        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionContract"/> class.
        /// </summary>
        /// <param name="contractName">An explicit contract name, when applicable. A <c>null</c> or empty string will result in a non-empty name being constructed based on default conventions.</param>
        /// <param name="type">The exported type identity.</param>
        public CompositionContract(string contractName, Type type)
        {
            Requires.NotNull(type, "type");

            this.ContractName = contractName; // ?? PartDiscovery.GetContractName(type);
            this.Type = type;
        }

        /// <summary>
        /// Gets the contract name.
        /// </summary>
        /// <value>A non-empty string.</value>
        public string ContractName { get; private set; }

        /// <summary>
        /// Gets the type of the exported value.
        /// </summary>
        public Type Type { get; private set; }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return this.Equals(obj as CompositionContract);
        }

        /// <inheritdoc />
        public bool Equals(CompositionContract other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Type.IsEquivalentTo(other.Type)
                && StringComparer.Ordinal.Equals(this.ContractName, other.ContractName);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            // Use the type's full name for its hash code rather than
            // calling Type.GetHashCode() directly. This allows contracts to
            // equivalent embeddedable types to generate the same hash code.
            return this.Type.FullName.GetHashCode() + (this.ContractName != null ? StringComparer.Ordinal.GetHashCode(this.ContractName) : 0);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string contractSuffix = this.ContractName != null
                ? " (" + this.ContractName + ")"
                : string.Empty;
            return ReflectionHelpers.GetTypeName(this.Type, false, true, null) + contractSuffix;
        }
    }
}
