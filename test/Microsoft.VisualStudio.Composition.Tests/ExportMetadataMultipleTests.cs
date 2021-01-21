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

    [Trait("Metadata", "Multiple")]
    public class ExportMetadataMultipleTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPart), typeof(PartWithMultipleMetadata))]
        public void MultipleExportMetadataValuesForOneKey(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            var metadataValue = Assert.IsType<string[]>(importingPart.ImportingProperty?.Metadata["SomeName"]);
            Assert.Equal(2, metadataValue.Length);
            Assert.Contains("b", metadataValue);
            Assert.Contains("c", metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPart), typeof(PartWithMultipleCustomMetadata))]
        public void MultipleCustomExportMetadataValuesForOneKey(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            var metadataValue = Assert.IsType<string[]>(importingPart.CustomMetadataImport?.Metadata["SomeName"]);
            Assert.Equal(2, metadataValue.Length);
            Assert.Contains("b", metadataValue);
            Assert.Contains("c", metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPart), typeof(PartWithSingleCustomMetadata))]
        public void SingleCustomExportMetadataValuesForOneKeyV1(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            var metadataValue = Assert.IsType<string[]>(importingPart.SingleCustomMetadataImport?.Metadata["SomeName"]);
            Assert.Equal(1, metadataValue.Length);
            Assert.Contains("b", metadataValue);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ImportingPart), typeof(PartWithSingleCustomMetadata))]
        public void SingleCustomExportMetadataValuesForOneKeyV2(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            object? metadataValue = importingPart.SingleCustomMetadataImport?.Metadata["SomeName"];
            Assert.Equal("b", metadataValue);
        }

        [Export]
        [ExportMetadata("SomeName", "b")]
        [ExportMetadata("SomeName", "c")]
        [MefV1.Export]
        [MefV1.ExportMetadata("SomeName", "b", IsMultiple = true)]
        [MefV1.ExportMetadata("SomeName", "c", IsMultiple = true)]
        public class PartWithMultipleMetadata { }

        [Export]
        [MefV1.Export]
        [CustomMetadata(SomeName = "b")]
        public class PartWithSingleCustomMetadata { }

        [Export]
        [MefV1.Export]
        [CustomMetadata(SomeName = "b")]
        [CustomMetadata(SomeName = "c")]
        public class PartWithMultipleCustomMetadata { }

        [Export]
        [MefV1.Export]
        public class ImportingPart
        {
            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithMultipleMetadata, IDictionary<string, object?>>? ImportingProperty { get; set; }

            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithMultipleCustomMetadata, IDictionary<string, object?>>? CustomMetadataImport { get; set; }

            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithSingleCustomMetadata, IDictionary<string, object?>>? SingleCustomMetadataImport { get; set; }
        }

        [MetadataAttribute, MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
        public class CustomMetadataAttribute : Attribute
        {
            public string? SomeName { get; set; }
        }
    }
}
