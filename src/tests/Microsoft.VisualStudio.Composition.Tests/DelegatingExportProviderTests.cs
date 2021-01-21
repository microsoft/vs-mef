// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class DelegatingExportProviderTests
    {
        [MefFact(CompositionEngines.V3EmulatingV2)]
        public void DelegatingExportProviderForwardsQueries(IContainer container)
        {
            var innerExportProvider = container.GetExportedValue<ExportProvider>();
            var delegatingExportProvider = new FilteringExportProvider(innerExportProvider);

            var unfilteredResults = innerExportProvider.GetExportedValues<int>();
            Assert.Equal(2, unfilteredResults.Count());
            Assert.True(unfilteredResults.Contains(1));
            Assert.True(unfilteredResults.Contains(2));

            var filteredResults = delegatingExportProvider.GetExportedValues<int>();
            Assert.Equal(2, filteredResults.Single());
        }

        [MefFact(CompositionEngines.V3EmulatingV2)]
        [Trait("Disposal", "")]
        public void DelegatingExportProviderDisposalDoesNotDisposeInner(IContainer container)
        {
            var innerExportProvider = container.GetExportedValue<ExportProvider>();
            var delegatingExportProvider = new FilteringExportProvider(innerExportProvider);

            delegatingExportProvider.Dispose();

            // Verify that the inner is still functional.
            var unfilteredResults = innerExportProvider.GetExportedValues<int>();
            Assert.Equal(2, unfilteredResults.Count());
        }

        public class ExportingPart
        {
            [Export]
            public int Value1 { get { return 1; } }

            [Export, ExportMetadata("a", "b")]
            public int Value2 { get { return 2; } }
        }

        private class FilteringExportProvider : DelegatingExportProvider
        {
            internal FilteringExportProvider(ExportProvider inner)
                : base(inner)
            {
            }

            public override IEnumerable<Export> GetExports(ImportDefinition importDefinition)
            {
                var modifiedImportDefinition = importDefinition.AddExportConstraint(
                    new ExportMetadataValueImportConstraint("a", "b"));
                return base.GetExports(modifiedImportDefinition);
            }
        }
    }
}
