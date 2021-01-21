// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ImportDefinitionTests
    {
        [Fact]
        public void AddExportConstraint()
        {
            var importDefinition = new ImportDefinition(
                "someContract",
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary<string, object?>.Empty,
                ImmutableHashSet.Create<IImportSatisfiabilityConstraint>(new ExportMetadataValueImportConstraint("a", "b")));

            var newConstraint = new ExportMetadataValueImportConstraint("c", "d");
            var modified = importDefinition.AddExportConstraint(newConstraint);
            Assert.Equal(2, modified.ExportConstraints.Count);
            Assert.True(modified.ExportConstraints.Contains(newConstraint));
            Assert.True(modified.ExportConstraints.Contains(importDefinition.ExportConstraints.Single()));

            // Also check that the rest was cloned properly.
            Assert.Equal(modified.ContractName, importDefinition.ContractName);
            Assert.Equal(modified.Cardinality, importDefinition.Cardinality);
            Assert.Same(modified.Metadata, importDefinition.Metadata);
            Assert.Same(modified.ExportFactorySharingBoundaries, importDefinition.ExportFactorySharingBoundaries);
        }

        [Fact]
        public void WithExportConstraints()
        {
            var importDefinition = new ImportDefinition(
                "someContract",
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary<string, object?>.Empty,
                ImmutableHashSet.Create<IImportSatisfiabilityConstraint>(new ExportMetadataValueImportConstraint("c", "d")));

            var newConstraints = ImmutableHashSet.Create<IImportSatisfiabilityConstraint>(new ExportMetadataValueImportConstraint("a", "b"));
            var modified = importDefinition.WithExportConstraints(newConstraints);
            Assert.True(newConstraints.SetEquals(modified.ExportConstraints));

            // Also check that the rest was cloned properly.
            Assert.Equal(modified.ContractName, importDefinition.ContractName);
            Assert.Equal(modified.Cardinality, importDefinition.Cardinality);
            Assert.Same(modified.Metadata, importDefinition.Metadata);
            Assert.Same(modified.ExportFactorySharingBoundaries, importDefinition.ExportFactorySharingBoundaries);
        }
    }
}
