namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    [DebuggerDisplay("{Contract.Type.Name,nq} ({Cardinality})")]
    public class ImportDefinition : IEquatable<ImportDefinition>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v2 attributes.
        /// </summary>
        public ImportDefinition(string contractName, ImportCardinality cardinality, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints, IReadOnlyCollection<string> exportFactorySharingBoundaries)
        {
            Requires.NotNullOrEmpty(contractName, "contractName");
            Requires.NotNull(metadata, "metadata");
            Requires.NotNull(additionalConstraints, "additionalConstraints");
            Requires.NotNull(exportFactorySharingBoundaries, "exportFactorySharingBoundaries");

            this.ContractName = contractName;
            this.Cardinality = cardinality;
            this.Metadata = metadata;
            this.ExportContraints = additionalConstraints;
            this.ExportFactorySharingBoundaries = exportFactorySharingBoundaries;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v1 attributes.
        /// </summary>
        public ImportDefinition(string contractName, ImportCardinality cardinality, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints)
            : this(contractName, cardinality, metadata, additionalConstraints, ImmutableHashSet.Create<string>())
        {
        }

        public string ContractName { get; private set; }

        public ImportCardinality Cardinality { get; private set; }

        /// <summary>
        /// Gets the sharing boundaries created when the export factory is used.
        /// </summary>
        public IReadOnlyCollection<string> ExportFactorySharingBoundaries { get; private set; }

        public IReadOnlyDictionary<string, object> Metadata { get; private set; }

        public IReadOnlyCollection<IImportSatisfiabilityConstraint> ExportContraints { get; private set; }

        public override int GetHashCode()
        {
            return this.ContractName.GetHashCode();
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

            return this.ContractName == other.ContractName
                && this.Cardinality == other.Cardinality;
        }
    }
}
