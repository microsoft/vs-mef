namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    /// <summary>
    /// A constraint that may be included in an <see cref="ImportDefinition"/> that only matches
    /// exports whose parts have a compatible <see cref="CreationPolicy"/>.
    /// </summary>
    public class PartCreationPolicyConstraint : IImportSatisfiabilityConstraint
    {
        private readonly CreationPolicy requiredCreationPolicy;

        private PartCreationPolicyConstraint(CreationPolicy creationPolicy)
        {
            this.requiredCreationPolicy = creationPolicy;
        }

        /// <summary>
        /// The constraint to include in the <see cref="ImportDefinition"/> when a shared part is required.
        /// </summary>
        public static readonly PartCreationPolicyConstraint SharedPartRequired = new PartCreationPolicyConstraint(CreationPolicy.Shared);

        /// <summary>
        /// The constraint to include in the <see cref="ImportDefinition"/> when a non-shared part is required.
        /// </summary>
        public static readonly PartCreationPolicyConstraint NonSharedPartRequired = new PartCreationPolicyConstraint(CreationPolicy.NonShared);

        /// <summary>
        /// Gets a dictionary of metadata to include in an <see cref="ExportDefinition"/> to signify the exporting part's CreationPolicy.
        /// </summary>
        /// <param name="partCreationPolicy">The <see cref="CreationPolicy"/> of the exporting <see cref="ComposablePartDefinition"/>.</param>
        /// <returns>A dictionary of metadata.</returns>
        public static ImmutableDictionary<string, object> GetExportMetadata(CreationPolicy partCreationPolicy)
        {
            var result = ImmutableDictionary.Create<string, object>();

            // As an optimization, only specify the export metadata if the policy isn't Any.
            // This matches our logic in IsSatisfiedBy that interprets no metadata as no part creation policy.
            if (partCreationPolicy != CreationPolicy.Any)
            {
                result = result.Add(CompositionConstants.PartCreationPolicyMetadataName, partCreationPolicy);
            }

            return result;
        }

        /// <summary>
        /// Creates a set of constraints to apply to an import given its required part creation policy.
        /// </summary>
        public static ImmutableHashSet<IImportSatisfiabilityConstraint> GetRequiredCreationPolicyConstraints(CreationPolicy requiredCreationPolicy)
        {
            var result = ImmutableHashSet.Create<IImportSatisfiabilityConstraint>();

            switch (requiredCreationPolicy)
            {
                case CreationPolicy.Shared:
                    result = result.Add(SharedPartRequired);
                    break;
                case CreationPolicy.NonShared:
                    result = result.Add(NonSharedPartRequired);
                    break;
                case CreationPolicy.Any:
                default:
                    break;
            }

            return result;
        }

        public static bool IsNonSharedInstanceRequired(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            return importDefinition.ExportContraints.Contains(NonSharedPartRequired);
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");

            object value;
            if (exportDefinition.Metadata.TryGetValue(CompositionConstants.PartCreationPolicyMetadataName, out value))
            {
                var partCreationPolicy = (CreationPolicy)value;
                return partCreationPolicy == CreationPolicy.Any
                    || partCreationPolicy == this.requiredCreationPolicy;
            }

            // No policy => our requirements are met
            return true;
        }
    }
}