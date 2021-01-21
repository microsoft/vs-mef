// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// A constraint that may be included in an <see cref="ImportDefinition"/> that only matches
    /// exports whose parts have a compatible <see cref="CreationPolicy"/>.
    /// </summary>
    public class PartCreationPolicyConstraint : IImportSatisfiabilityConstraint, IDescriptiveToString
    {
        /// <summary>
        /// The constraint to include in the <see cref="ImportDefinition"/> when a shared part is required.
        /// </summary>
        public static readonly PartCreationPolicyConstraint SharedPartRequired = new PartCreationPolicyConstraint(CreationPolicy.Shared);

        /// <summary>
        /// The constraint to include in the <see cref="ImportDefinition"/> when a non-shared part is required.
        /// </summary>
        public static readonly PartCreationPolicyConstraint NonSharedPartRequired = new PartCreationPolicyConstraint(CreationPolicy.NonShared);

        private PartCreationPolicyConstraint(CreationPolicy creationPolicy)
        {
            this.RequiredCreationPolicy = creationPolicy;
        }

        public CreationPolicy RequiredCreationPolicy { get; private set; }

        /// <summary>
        /// Gets a dictionary of metadata to include in an <see cref="ExportDefinition"/> to signify the exporting part's CreationPolicy.
        /// </summary>
        /// <param name="partCreationPolicy">The <see cref="CreationPolicy"/> of the exporting <see cref="ComposablePartDefinition"/>.</param>
        /// <returns>A dictionary of metadata.</returns>
        public static ImmutableDictionary<string, object?> GetExportMetadata(CreationPolicy partCreationPolicy)
        {
            var result = ImmutableDictionary.Create<string, object?>();

            // As an optimization, only specify the export metadata if the policy isn't Any.
            // This matches our logic in IsSatisfiedBy that interprets no metadata as no part creation policy.
            if (partCreationPolicy != CreationPolicy.Any)
            {
                result = result.Add(CompositionConstants.PartCreationPolicyMetadataName, partCreationPolicy);
            }

            return result;
        }

        public static PartCreationPolicyConstraint? GetRequiredCreationPolicyConstraint(CreationPolicy requiredCreationPolicy)
        {
            switch (requiredCreationPolicy)
            {
                case CreationPolicy.Shared:
                    return SharedPartRequired;
                case CreationPolicy.NonShared:
                    return NonSharedPartRequired;
                case CreationPolicy.Any:
                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates a set of constraints to apply to an import given its required part creation policy.
        /// </summary>
        public static ImmutableHashSet<IImportSatisfiabilityConstraint> GetRequiredCreationPolicyConstraints(CreationPolicy requiredCreationPolicy)
        {
            var result = ImmutableHashSet.Create<IImportSatisfiabilityConstraint>();
            var constraint = GetRequiredCreationPolicyConstraint(requiredCreationPolicy);
            if (constraint != null)
            {
                result = result.Add(constraint);
            }

            return result;
        }

        public static bool IsNonSharedInstanceRequired(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));

            return importDefinition.ExportConstraints.Contains(NonSharedPartRequired);
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, nameof(exportDefinition));

            object? value;
            if (exportDefinition.Metadata.TryGetValue(CompositionConstants.PartCreationPolicyMetadataName, out value) && value is object)
            {
                var partCreationPolicy = (CreationPolicy)value;
                return partCreationPolicy == CreationPolicy.Any
                    || partCreationPolicy == this.RequiredCreationPolicy;
            }

            // No policy => our requirements are met
            return true;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            indentingWriter.WriteLine("RequiredCreationPolicy: {0}", this.RequiredCreationPolicy);
        }

        public bool Equals(IImportSatisfiabilityConstraint? obj)
        {
            var other = obj as PartCreationPolicyConstraint;
            if (other == null)
            {
                return false;
            }

            return this.RequiredCreationPolicy == other.RequiredCreationPolicy;
        }
    }
}