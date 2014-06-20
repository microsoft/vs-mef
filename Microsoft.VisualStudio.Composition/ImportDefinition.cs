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
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints, IReadOnlyCollection<string> exportFactorySharingBoundaries)
        {
            Requires.NotNull(contract, "contract");
            Requires.NotNull(additionalConstraints, "additionalConstraints");
            Requires.NotNull(exportFactorySharingBoundaries, "exportFactorySharingBoundaries");

            this.Contract = contract;
            this.Cardinality = cardinality;
            this.ExportContraints = additionalConstraints;
            this.RequiredCreationPolicy = CreationPolicy.Any;
            this.ExportFactorySharingBoundaries = exportFactorySharingBoundaries;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v1 attributes.
        /// </summary>
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints, CreationPolicy requiredCreationPolicy)
            : this(contract, cardinality, additionalConstraints, ImmutableHashSet.Create<string>())
        {
            this.RequiredCreationPolicy = requiredCreationPolicy;
        }

        public ImportCardinality Cardinality { get; private set; }

        public CreationPolicy RequiredCreationPolicy { get; private set; }

        public Type TypeIdentity
        {
            get { return this.Contract.Type; }
        }

        /// <summary>
        /// Gets the sharing boundaries created when the export factory is used.
        /// </summary>
        public IReadOnlyCollection<string> ExportFactorySharingBoundaries { get; private set; }

        public CompositionContract Contract { get; private set; }

        public IReadOnlyCollection<IImportSatisfiabilityConstraint> ExportContraints { get; private set; }

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
