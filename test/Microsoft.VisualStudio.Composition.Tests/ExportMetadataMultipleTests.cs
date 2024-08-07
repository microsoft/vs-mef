﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ImportingPart), typeof(PartWithMultipleStringArrayCustomMetadata))]
        public void MultipleCustomExportMetadataValuesWithStringArray(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            string[] metadataValue = Assert.IsType<string[]>(importingPart.CustomStringArrayMetadataImport?.Metadata["SomeName"]);
            Assert.Equal(2, metadataValue.Length);
            Assert.Contains("b", metadataValue);
            Assert.Contains("c", metadataValue);

            string[][] stringArrayValue = Assert.IsType<string[][]>(importingPart.CustomStringArrayMetadataImport?.Metadata["StringArray"]);
            Assert.Equal(2, stringArrayValue.Length);
            int bIndex = metadataValue[0] == "b" ? 0 : 1;
            int cIndex = bIndex == 0 ? 1 : 0;
            Assert.Empty(stringArrayValue[cIndex]);
            Assert.True(new string[] { "First", "Second" }.SequenceEqual(stringArrayValue[bIndex]));
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ImportingPartViaMetadataView), typeof(PartWithMultipleStringArrayCustomMetadata))]
        public void MultipleCustomExportMetadataValuesWithStringArray_MetadataView(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartViaMetadataView>();
            string?[]? metadataValue = importingPart.CustomStringArrayMetadataViewImport?.Metadata.SomeName;
            Assert.NotNull(metadataValue);
            Assert.Equal(2, metadataValue.Length);
            Assert.Contains("b", metadataValue);
            Assert.Contains("c", metadataValue);

            string[][]? stringArrayValue = importingPart.CustomStringArrayMetadataViewImport?.Metadata.StringArray;
            Assert.NotNull(stringArrayValue);
            Assert.Equal(2, stringArrayValue.Length);
            int bIndex = metadataValue[0] == "b" ? 0 : 1;
            int cIndex = bIndex == 0 ? 1 : 0;
            Assert.Empty(stringArrayValue[cIndex]);
            Assert.True(new string[] { "First", "Second" }.SequenceEqual(stringArrayValue[bIndex]));
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPart), typeof(PartWithSingleCustomMetadata))]
        public void SingleCustomExportMetadataValuesForOneKeyV1(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPart>();
            var metadataValue = Assert.IsType<string[]>(importingPart.SingleCustomMetadataImport?.Metadata["SomeName"]);
            Assert.Single(metadataValue);
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
        [CustomMetadataWithStringArray(SomeName = "b", StringArray = ["First", "Second"])]
        [CustomMetadataWithStringArray(SomeName = "c")]
        public class PartWithMultipleStringArrayCustomMetadata { }

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
            public Lazy<PartWithMultipleStringArrayCustomMetadata, IDictionary<string, object?>>? CustomStringArrayMetadataImport { get; set; }

            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithSingleCustomMetadata, IDictionary<string, object?>>? SingleCustomMetadataImport { get; set; }
        }

        [Export]
        [MefV1.Export]
        public class ImportingPartViaMetadataView
        {
            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<PartWithMultipleStringArrayCustomMetadata, ICustomMetadataWithStringArray>? CustomStringArrayMetadataViewImport { get; set; }
        }

        [MetadataAttribute, MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
        public class CustomMetadataAttribute : Attribute
        {
            public string? SomeName { get; set; }
        }

        [MetadataAttribute, MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
        public class CustomMetadataWithStringArrayAttribute : CustomMetadataAttribute
        {
            public string[] StringArray { get; set; } = [];
        }

        public interface ICustomMetadataWithStringArray
        {
            string?[] SomeName { get; }

            string[][] StringArray { get; }
        }
    }
}
