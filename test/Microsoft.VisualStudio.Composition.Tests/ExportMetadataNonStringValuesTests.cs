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
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Metadata", "NonStringValues")]
    public class ExportMetadataNonStringValuesTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithIntMetadataValues), typeof(ImportingPart))]
        public void ExportMetadataWithIntValues(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            object metadataValue = importingPart.ImportingProperty.Metadata["a"];
            Assert.IsType<int>(metadataValue);
            Assert.Equal(5, metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithNegativeIntMetadataValues), typeof(ImportingPart))]
        public void ExportMetadataWithNegativeIntValues(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            object metadataValue = importingPart.NegativeMetadataImport.Metadata["a"];
            Assert.IsType<int>(metadataValue);
            Assert.Equal(-5, metadataValue);
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("a", 5)]
        [MefV1.ExportMetadata("a", 5)]
        public class PartWithIntMetadataValues { }

        [Export]
        [MefV1.Export]
        [ExportMetadata("a", -5)]
        [MefV1.ExportMetadata("a", -5)]
        public class PartWithNegativeIntMetadataValues { }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithIntMetadataValues, IDictionary<string, object>> ImportingProperty { get; set; } = null!;

            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithNegativeIntMetadataValues, IDictionary<string, object>> NegativeMetadataImport { get; set; } = null!;
        }
    }
}
