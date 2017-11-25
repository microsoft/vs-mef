// Copyright (c) Microsoft. All rights reserved.

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
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ExportedTypeWithAllowMultipleDerivedMetadata), typeof(PartThatImportsExportWithDerivedMetadata))]
        public void CustomMetadataOnAllowMultipleDerivedMetadataAttributeOnExportedType(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsExportWithDerivedMetadata>();
            Assert.IsType<string[]>(part.ImportingAllowMultiple.Metadata["Name"]);
            var array = (string[])part.ImportingAllowMultiple.Metadata["Name"];
            Assert.Equal(2, array.Length);
            Assert.True(array.Contains("Andrew1"));
            Assert.True(array.Contains("Andrew2"));
        }

        // BUGBUG: MEFv2 throws NullReferenceException in this case.
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(ImportingPart), typeof(ExportedTypeWithMetadata), typeof(ExportedTypeWithMultipleMetadata), typeof(TypeWithExportingMemberAndMetadata))]
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

        // BUGBUG: MEFv2 throws NullReferenceException in this case.
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2,
            typeof(ExportWithMultipleMetadataAttributesAndComplementingMetadataValues), typeof(PartThatImportsAnExportWithMultipleComplementingMetadata))]
        public void ComplementingMetadataDefinedInTwoAttributes(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsAnExportWithMultipleComplementingMetadata>();

            object after = part.ImportingProperty.Metadata["After"];
            Assert.IsType<string[]>(after);
            var afterArray = (string[])after;
            Assert.True(afterArray.Contains(null));
            Assert.True(afterArray.Contains("AfterValue"));

            object before = part.ImportingProperty.Metadata["Before"];
            Assert.IsType<string[]>(before);
            var beforeArray = (string[])before;
            Assert.True(beforeArray.Contains(null));
            Assert.True(beforeArray.Contains("BeforeValue"));
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportWithDerivedExportMetadata), typeof(ImportingPartForDerivedExportMetadata))]
        public void BasePropertiesAreNotIgnoredInMefV1(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPartForDerivedExportMetadata>();

            object prop1 = part.ImportingProperty.Metadata["Property"];
            Assert.IsType<string[]>(prop1);
            var prop1Array = (string[])prop1;
            Assert.Contains("prop1", prop1Array);

            object prop2 = part.ImportingProperty.Metadata["AnotherProperty"];
            Assert.IsType<string[]>(prop2);
            var prop2Array = (string[])prop2;
            Assert.Contains("prop2", prop2Array);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportUsingDerivedExportAttribute), typeof(PartThatImportsSingleDerivedExportAttributes))]
        public void BasePropertiesForExportAttributeAreIgnoredInMefV1(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsSingleDerivedExportAttributes>();

            foreach (var propertyName in typeof(ExportAttribute).GetTypeInfo().GetProperties().Select(p => p.Name))
            {
                Assert.DoesNotContain(propertyName, part.ImportingProperty.Metadata.Keys);
            }

            object somePropertyValue = part.ImportingProperty.Metadata["SomeProperty"];
            Assert.IsType<string>(somePropertyValue);
            Assert.Equal("SomePropertyValue", somePropertyValue.ToString());
            Assert.NotNull(part.ImportingProperty.Value);
        }

        // The MEF v2 behavior is that properties from a base class do not get included in the metadata. Currently, our MEF v2 discovery does include them. After fixing this, we can enable the test for MEF v2 compat.
        [MefFact(CompositionEngines.V2, typeof(ExportWithDerivedExportMetadata), typeof(ImportingPartForDerivedExportMetadata))]
        public void BasePropertiesAreIgnoredInMefV2(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPartForDerivedExportMetadata>();

            Assert.False(part.ImportingProperty.Metadata.ContainsKey("Property"));

            object prop2 = part.ImportingProperty.Metadata["AnotherProperty"];
            Assert.Equal("prop2", prop2.ToString());
        }

#if DESKTOP
        [Fact]
        public async Task PartWithAttributesDefiningSamePropertyContainsDiscoveryErrorsForV1()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V1Discovery.CreatePartsAsync(typeof(PartThatExportsWithMetadataThatHasMatchingProperties)));
            Assert.NotEmpty(catalog.DiscoveredParts.DiscoveryErrors);
            PartDiscoveryException exception = catalog.DiscoveredParts.DiscoveryErrors.First();
            Assert.IsType<NotSupportedException>(exception.InnerException);
        }
#endif

        [Fact]
        public async Task PartWithAttributesDefiningSamePropertyDoesNotContainDiscoveryErrorsForV2()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(
                await TestUtilities.V2Discovery.CreatePartsAsync(typeof(PartThatExportsWithMetadataThatHasMatchingProperties)));
            Assert.Empty(catalog.DiscoveredParts.DiscoveryErrors);
        }

        [MefV1.Export, Export]
        [DerivedMetadata("prop1", "prop2")]
        public class ExportWithDerivedExportMetadata
        {
        }

        [MefV1.Export, Export]
        public class ImportingPartForDerivedExportMetadata
        {
            [MefV1.Import, Import]
            public Lazy<ExportWithDerivedExportMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export, MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ImportingPart
        {
            [Import, MefV1.Import]
            public Lazy<ExportedTypeWithMetadata, IDictionary<string, object>> ImportOfType { get; set; }

            [Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]
            public Lazy<ExportedTypeWithMultipleMetadata, IDictionary<string, object>> ImportOfTypeWithMultipleMetadata { get; set; }

            [Import, MefV1.Import]
            public Lazy<string, IDictionary<string, object>> ImportOfProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        [NameAndAge(Name = "Andrew", Age = "4")]
        public class ExportedTypeWithMetadata { }

        [MefV1.Export]
        [Export]
        [NameDerived(Name = "Andrew")]
        public class ExportedTypeWithDerivedMetadata { }

        [MefV1.Export]
        [Export]
        [NameMultipleDerived(Name = "Andrew1")]
        [NameMultipleDerived(Name = "Andrew2")]
        public class ExportedTypeWithAllowMultipleDerivedMetadata { }

        [MefV1.Export]
        [Export]
        public class PartThatImportsExportWithDerivedMetadata
        {
            [Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]
            public Lazy<ExportedTypeWithDerivedMetadata, IDictionary<string, object>> ImportingProperty { get; set; }

            [Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]
            public Lazy<ExportedTypeWithAllowMultipleDerivedMetadata, IDictionary<string, object>> ImportingAllowMultiple { get; set; }
        }

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
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
        /// An export with two instances of a custom export metadata attribute applied,
        /// each with different properties set.
        /// </summary>
        [MefV1.Export]
        [Export]
        [Order(After = "AfterValue")]
        [Order(Before = "BeforeValue")]
        public class ExportWithMultipleMetadataAttributesAndComplementingMetadataValues { }

        [MefV1.Export]
        [Export]
        public class PartThatImportsAnExportWithMultipleComplementingMetadata
        {
            [Import, MefV1.Import]
            public Lazy<ExportWithMultipleMetadataAttributesAndComplementingMetadataValues, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [DerivedFromExportAttribute("SomePropertyValue")]
        [MefV1DerivedFromExportAttribute("SomePropertyValue")]
        public class ExportUsingDerivedExportAttribute { }

        [Export, MefV1.Export]
        public class PartThatImportsSingleDerivedExportAttributes
        {
            [Import, MefV1.Import]
            public Lazy<ExportUsingDerivedExportAttribute, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [FirstAttributeWithIdenticalProperties("SomeValue")]
        [SecondAttributeWithIdenticalProperties("SomeOtherValue")]
        [Export, MefV1.Export]
        public class PartThatExportsWithMetadataThatHasMatchingProperties { }

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
        /// An attribute that derives from AllowMultiple base class
        /// and intentionally does not have <see cref="AttributeUsageAttribute"/> attributes applied directly.
        /// </summary>
        [MetadataAttribute] // only V2 needs this
        public class NameMultipleDerivedAttribute : NameMultipleAttribute
        {
            public string OtherProperty { get; set; }
        }

        /// <summary>
        /// An export metadata attribute that derives from another,
        /// and intentionally does not have <see cref="MetadataAttributeAttribute"/> applied directly.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class NameDerivedAttribute : NameMultipleAttribute
        {
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        [MetadataAttribute]
        public class DerivedFromExportAttributeAttribute : ExportAttribute
        {
            public DerivedFromExportAttributeAttribute()
                : this(string.Empty) { }

            public DerivedFromExportAttributeAttribute(string someProperty)
                : base()
            {
                this.SomeProperty = someProperty;
            }

            public DerivedFromExportAttributeAttribute(string someProperty, Type t)
                : base(t)
            {
                this.SomeProperty = someProperty;
            }

            public string SomeProperty { get; set; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        [MefV1.MetadataAttribute]
        public class MefV1DerivedFromExportAttributeAttribute : MefV1.ExportAttribute
        {
            public MefV1DerivedFromExportAttributeAttribute()
                : this(string.Empty) { }

            public MefV1DerivedFromExportAttributeAttribute(string someProperty)
                : base()
            {
                this.SomeProperty = someProperty;
            }

            public MefV1DerivedFromExportAttributeAttribute(string someProperty, Type t)
                : base(t)
            {
                this.SomeProperty = someProperty;
            }

            public string SomeProperty { get; set; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true)]
        [MefV1.MetadataAttribute, MetadataAttribute]
        public class BaseMetadataAttribute : Attribute
        {
            public BaseMetadataAttribute(string property)
            {
                this.Property = property;
            }

            public string Property { get; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false)]
        [MefV1.MetadataAttribute, MetadataAttribute]
        public class OtherMetadataAttribute : Attribute
        {
            public OtherMetadataAttribute(string property)
            {
                this.Property = property;
            }

            public string Property { get; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true)]
        [MefV1.MetadataAttribute, MetadataAttribute]
        public class DerivedMetadataAttribute : BaseMetadataAttribute
        {
            public DerivedMetadataAttribute(string property, string anotherProperty)
                : base(property)
            {
                this.AnotherProperty = anotherProperty;
            }

            public string AnotherProperty { get; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
        [MetadataAttribute, MefV1.MetadataAttribute]
        public class OrderAttribute : Attribute
        {
            public string Before { get; set; }

            public string After { get; set; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        [MetadataAttribute, MefV1.MetadataAttribute]
        public class FirstAttributeWithIdenticalPropertiesAttribute : Attribute
        {
            public string Property { get; set; }

            public FirstAttributeWithIdenticalPropertiesAttribute(string property)
            {
                this.Property = property;
            }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        [MetadataAttribute, MefV1.MetadataAttribute]
        public class SecondAttributeWithIdenticalPropertiesAttribute : Attribute
        {
            public string Property { get; set; }

            public SecondAttributeWithIdenticalPropertiesAttribute(string property)
            {
                this.Property = property;
            }
        }

        #region CustomMetadataAttributeLotsOfTypesAndVisibilities test

        [MefFact(CompositionEngines.V1Compat, typeof(PartThatImportsLotsOfTypesAndVisibilitiesAttribute), typeof(PartWithLotsOfTypesAndVisibilitiesAttribute))]
        public void CustomMetadataAttributeLotsOfTypesAndVisibilitiesV1(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsLotsOfTypesAndVisibilitiesAttribute>();
            Assert.Equal(true, part.ImportingProperty.Metadata["PublicProperty"]);
            Assert.False(part.ImportingProperty.Metadata.ContainsKey("PublicField"));
            Assert.False(part.ImportingProperty.Metadata.ContainsKey("InternalProperty"));
            Assert.False(part.ImportingProperty.Metadata.ContainsKey("InternalField"));
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartThatImportsLotsOfTypesAndVisibilitiesAttribute), typeof(PartWithLotsOfTypesAndVisibilitiesAttribute))]
        public void CustomMetadataAttributeLotsOfTypesAndVisibilitiesV2(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsLotsOfTypesAndVisibilitiesAttribute>();
            Assert.Equal(true, part.ImportingProperty.Metadata["PublicProperty"]);
            Assert.False(part.ImportingProperty.Metadata.ContainsKey("PublicField"));
            Assert.Equal(true, part.ImportingProperty.Metadata["InternalProperty"]);
            Assert.False(part.ImportingProperty.Metadata.ContainsKey("InternalField"));
        }

        [Export, MefV1.Export]
        public class PartThatImportsLotsOfTypesAndVisibilitiesAttribute
        {
            [Import, MefV1.Import]
            public Lazy<PartWithLotsOfTypesAndVisibilitiesAttribute, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export, MefV1.Export]
        [LotsOfTypesAndVisibilities]
        public class PartWithLotsOfTypesAndVisibilitiesAttribute { }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        [MetadataAttribute, MefV1.MetadataAttribute]
        public class LotsOfTypesAndVisibilitiesAttribute : Attribute
        {
            public bool PublicProperty { get { return true; } }

            public bool PublicField = true;

            internal bool InternalProperty { get { return true; } }

            internal bool InternalField = true;
        }

        #endregion
    }
}
