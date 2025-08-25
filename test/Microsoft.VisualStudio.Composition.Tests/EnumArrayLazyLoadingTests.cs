// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Metadata", "EnumArray")]
    [Trait("Efficiency", "LazyLoad")]
    public class EnumArrayLazyLoadingTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPartWithEnumArray), typeof(ExportWithMultipleEnumMetadata))]
        public void EnumArrayMetadataLazyLoadingWithAllowMultiple(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithEnumArray>();

            // Verify we can access the enum array metadata
            var enumArray = Assert.IsType<TestEnumForLazyLoading[]>(importingPart.ImportingProperty?.Metadata["TestEnumArray"]);
            Assert.Equal(2, enumArray.Length);
            Assert.Contains(TestEnumForLazyLoading.Value1, enumArray);
            Assert.Contains(TestEnumForLazyLoading.Value2, enumArray);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartWithEnumArray), typeof(ExportWithSingleEnumMetadata))]
        public void SingleEnumInArrayMetadataLazyLoadingV1(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithEnumArray>();

            // In V1, even a single enum value with AllowMultiple=true gets wrapped in an array
            var enumArray = Assert.IsType<TestEnumForLazyLoading[]>(importingPart.SingleEnumMetadataImport?.Metadata["TestEnumArray"]);
            Assert.Single(enumArray);
            Assert.Equal(TestEnumForLazyLoading.Value1, enumArray[0]);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ImportingPartWithEnumArray), typeof(ExportWithSingleEnumMetadata))]
        public void SingleEnumInArrayMetadataLazyLoadingV2(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithEnumArray>();

            // In V2, a single enum value with AllowMultiple=true remains a single value
            var enumValue = Assert.IsType<TestEnumForLazyLoading>(importingPart.SingleEnumMetadataImport?.Metadata["TestEnumArray"]);
            Assert.Equal(TestEnumForLazyLoading.Value1, enumValue);
        }

        [Export]
        [MefV1.Export]
        public class ImportingPartWithEnumArray
        {
            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<ExportWithMultipleEnumMetadata, IDictionary<string, object?>>? ImportingProperty { get; set; }

            [Import(AllowDefault = true)]
            [MefV1.Import(AllowDefault = true)]
            public Lazy<ExportWithSingleEnumMetadata, IDictionary<string, object?>>? SingleEnumMetadataImport { get; set; }
        }

        [Export]
        [MefV1.Export]
        [TestEnumMetadata(TestEnumForLazyLoading.Value1)]
        [TestEnumMetadata(TestEnumForLazyLoading.Value2)]
        public class ExportWithMultipleEnumMetadata
        {
        }

        [Export]
        [MefV1.Export]
        [TestEnumMetadata(TestEnumForLazyLoading.Value1)]
        public class ExportWithSingleEnumMetadata
        {
        }

        [MetadataAttribute, MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class TestEnumMetadataAttribute : Attribute
        {
            public TestEnumMetadataAttribute(TestEnumForLazyLoading value)
            {
                this.TestEnumArray = value;
            }

            public TestEnumForLazyLoading TestEnumArray { get; }
        }

        public enum TestEnumForLazyLoading
        {
            Value1,
            Value2,
            Value3,
        }
    }
}