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

    public class CustomMetadataAttributeTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPart), typeof(ExportedTypeWithMetadata), typeof(TypeWithExportingMemberAndMetadata))]
        public void CustomMetadataOnExportedType(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal("Andrew", part.ImportOfType.Metadata["Name"]);
            Assert.Equal("4", part.ImportOfType.Metadata["Age"]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportedTypeWithDerivedMetadata), typeof(PartThatImportsExportWithDerivedMetadata))]
        public void CustomMetadataOnDerivedMetadataAttributeOnExportedTypeV1(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsExportWithDerivedMetadata>();
            Assert.Equal("Andrew", part.ImportingProperty.Metadata["Name"]);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(ExportedTypeWithDerivedMetadata), typeof(PartThatImportsExportWithDerivedMetadata))]
        public void CustomMetadataOnDerivedMetadataAttributeOnExportedTypeV2(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsExportWithDerivedMetadata>();
            Assert.False(part.ImportingProperty.Metadata.ContainsKey("Name"));
        }

        // BUGBUG: MEFv2 throws NullReferenceException in this case.
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2)]
        public void MultipleCustomMetadataOnExportedType(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.IsType<string[]>(part.ImportOfTypeWithMultipleMetadata.Metadata["Name"]);
            Assert.Equal(null, ((string[])part.ImportOfTypeWithMultipleMetadata.Metadata["Name"])[0]);
            Assert.Equal(null, ((string[])part.ImportOfTypeWithMultipleMetadata.Metadata["Name"])[1]);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPart), typeof(ExportedTypeWithMetadata), typeof(TypeWithExportingMemberAndMetadata))]
        public void CustomMetadataOnExportedProperty(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal("Andrew", part.ImportOfProperty.Metadata["Name"]);
            Assert.Equal("4", part.ImportOfProperty.Metadata["Age"]);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPart), typeof(ExportedTypeWithMetadata), typeof(TypeWithExportingMemberAndMetadata))]
        public void CustomMetadataDictionaryCaseSensitive(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.False(part.ImportOfType.Metadata.ContainsKey("name"));
        }

        [Export, MefV1.Export]
        public class ImportingPart
        {
            [Import, MefV1.Import]
            public Lazy<ExportedTypeWithMetadata, IDictionary<string, object>> ImportOfType { get; set; }

            [Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]
            public Lazy<ExportedTypeWithMultipleMetadata, IDictionary<string, object>> ImportOfTypeWithMultipleMetadata { get; set; }

            [Import, MefV1.Import]
            public Lazy<string, IDictionary<string, object>> ImportOfProperty { get; set; }
        }

        [MefV1.Export]
        [Export]
        [NameAndAge(Name = "Andrew", Age = "4")]
        public class ExportedTypeWithMetadata { }

        [MefV1.Export]
        [Export]
        [NameDerived(Name = "Andrew")]
        public class ExportedTypeWithDerivedMetadata { }

        [MefV1.Export]
        [Export]
        public class PartThatImportsExportWithDerivedMetadata
        {
            [Import, MefV1.Import]
            public Lazy<ExportedTypeWithDerivedMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        public class TypeWithExportingMemberAndMetadata
        {
            [MefV1.Export]
            [Export]
            [NameAndAge(Name = "Andrew", Age = "4")]
            public string SomeValue
            {
                get { return "Foo"; }
            }
        }

        [MefV1.Export]
        [Export]
        [NameMultiple(Name = null)] // these metadata values are intentionally NULL...
        [NameMultiple(Name = null)] // ...they test typing of the metadata value array when values are all null.
        public class ExportedTypeWithMultipleMetadata { }

        /// <summary>
        /// An attribute that exports "Name" metadata with IsMultiple=true (meaning the value is string[] instead of just string).
        /// </summary>
        [MetadataAttribute]
        [MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
        public class NameMultipleAttribute : Attribute
        {
            public string Name { get; set; }
        }

        /// <summary>
        /// An export metadata attribute that derives from another,
        /// and intentionally does not have <see cref="MetadataAttribute"/> applied directly.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class NameDerivedAttribute : NameMultipleAttribute
        {
        }
    }
}
