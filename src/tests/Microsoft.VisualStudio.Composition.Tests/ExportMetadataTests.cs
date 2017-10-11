// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Metadata", "")]
    public class ExportMetadataTests
    {
        [MefFact(CompositionEngines.V2Compat | CompositionEngines.V1Compat, typeof(ImportingPartWithMetadataDictionary), typeof(PartWithExportMetadata))]
        public void ImportWithMetadataDictionary(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithMetadataDictionary>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal("b", importingPart.ImportingProperty.Metadata["a"]);
            Assert.False(importingPart.ImportingProperty.IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingPartWithMetadataDictionary), typeof(PartWithExportMetadata))]
        [Trait("Efficiency", "InstanceReuse")]
        public void MetadataDictionaryInstanceSharedAcrossImports(IContainer container)
        {
            var importingPart1 = container.GetExportedValue<ImportingPartWithMetadataDictionary>();
            var importingPart2 = container.GetExportedValue<ImportingPartWithMetadataDictionary>();
            Assert.NotSame(importingPart1, importingPart2); // non-shared part is crucial to the integrity of this test.

            // Ensure that the dictionary instances are shared.
            Assert.Same(importingPart1.ImportingProperty.Metadata, importingPart2.ImportingProperty.Metadata);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartWithMetadataDictionary), typeof(PartWithExportMetadata))]
        public void MetadataDictionaryInstanceIsImmutable(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithMetadataDictionary>();
            Assert.Throws<NotSupportedException>(() => importingPart.ImportingProperty.Metadata["foo"] = 5);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportManyPartWithMetadataDictionary), typeof(PartWithExportMetadata))]
        public void ImportManyWithMetadataDictionary(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportManyPartWithMetadataDictionary>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count());
            Assert.Equal("b", importingPart.ImportingProperty.Single().Metadata["a"]);
            Assert.False(importingPart.ImportingProperty.Single().IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Single().Value);
        }

        [MefFact(CompositionEngines.V2Compat | CompositionEngines.V1Compat, typeof(ImportingPartWithMetadataClass), typeof(PartWithExportMetadata))]
        public void ImportWithMetadataClass(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithMetadataClass>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal("b", importingPart.ImportingProperty.Metadata.a);
            Assert.False(importingPart.ImportingProperty.IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V3EmulatingV2WithNonPublic | CompositionEngines.V1Compat, typeof(ImportingPartWithNonPublicMetadataClass), typeof(PartWithExportMetadata))]
        public void ImportWithNonPublicMetadataClass(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithNonPublicMetadataClass>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal("b", importingPart.ImportingProperty.Metadata.a);
            Assert.False(importingPart.ImportingProperty.IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V2Compat | CompositionEngines.V1Compat, typeof(ImportManyPartWithMetadataClass), typeof(PartWithExportMetadata))]
        public void ImportManyWithMetadataClass(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportManyPartWithMetadataClass>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count());
            Assert.Equal("b", importingPart.ImportingProperty.Single().Metadata.a);
            Assert.False(importingPart.ImportingProperty.Single().IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Single().Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartWithMetadataInterface), typeof(PartWithExportMetadata))]
        public void ImportWithMetadataInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportingPartWithMetadataInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal("b", importingPart.ImportingProperty.Metadata.a);
            Assert.False(importingPart.ImportingProperty.IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportManyPartWithMetadataInterface), typeof(PartWithExportMetadata))]
        public void ImportManyWithMetadataInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportManyPartWithMetadataInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count());
            Assert.Equal("b", importingPart.ImportingProperty.Single().Metadata.a);
            Assert.False(importingPart.ImportingProperty.Single().IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Single().Value);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ImportManyPartWithInternalMetadataInterface), typeof(PartWithExportMetadata))]
        public void ImportManyWithInternalMetadataInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<ImportManyPartWithInternalMetadataInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count());
            Assert.Equal("b", importingPart.ImportingProperty.Single().Metadata.a);
            Assert.Equal("internal!", importingPart.ImportingProperty.Single().Metadata.MetadataOnInternalInterface);
            Assert.False(importingPart.ImportingProperty.Single().IsValueCreated);
            Assert.IsType<PartWithExportMetadata>(importingPart.ImportingProperty.Single().Value);
        }

        [MefFact(CompositionEngines.Unspecified, typeof(ImportingPartWithMetadataInterface), typeof(PartWithExportMetadata))]
        [Trait("Efficiency", "InstanceReuse")]
        public void MetadataViewInterfaceInstanceSharedAcrossImports(IContainer container)
        {
            var importingPart1 = container.GetExportedValue<ImportingPartWithMetadataInterface>();
            var importingPart2 = container.GetExportedValue<ImportingPartWithMetadataInterface>();
            Assert.NotSame(importingPart1, importingPart2); // non-shared part is crucial to the integrity of this test.

            // Ensure that the interface instances are shared.
            Assert.Same(importingPart1.ImportingProperty.Metadata, importingPart2.ImportingProperty.Metadata);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportWithEnumMetadata), typeof(PartThatImportsEnumMetadata))]
        public void MetadataEnumValue(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsEnumMetadata>();
            object metadataValue = importer.ImportingProperty.Metadata["SomeName"];
            Assert.Equal(MetadataEnum.Value2, metadataValue);
            Assert.IsType<MetadataEnum>(metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportWithNonPublicEnumMetadata))]
        public void NonPublicMetadataEnumValue(IContainer container)
        {
            var part = container.GetExport<ExportWithNonPublicEnumMetadata, IDictionary<string, object>>();
            object metadataValue = part.Metadata["SomeName"];
            Assert.Equal(MetadataEnumNonPublic.Value2, metadataValue);
            Assert.IsType<MetadataEnumNonPublic>(metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportWithTypeMetadata), typeof(PartThatImportsTypeMetadata))]
        public void MetadataTypeValue(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsTypeMetadata>();
            object metadataValue = importer.ImportingProperty.Metadata["SomeName"];
            Assert.Equal(typeof(int), metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportWithCharMetadata), typeof(PartThatImportsCharMetadata))]
        public void MetadataCharValue(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsCharMetadata>();
            object metadataValue = importer.ImportingProperty.Metadata["SomeName"];
            Assert.Equal('a', metadataValue);
            Assert.IsType<char>(metadataValue);
            Assert.Equal('\'', importer.ImportingProperty.Metadata["Apostrophe"]);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportWithBoolMetadata), typeof(PartThatImportsBoolMetadata))]
        public void MetadataBoolValue(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsBoolMetadata>();
            object metadataValue = importer.ImportingProperty.Metadata["SomeName"];
            Assert.Equal(true, metadataValue);
            Assert.IsType<bool>(metadataValue);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithExportMetadata), typeof(ImportingPartWithMetadataDictionary))]
        public void ExportTypeIdentityMetadataIsPresent(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPartWithMetadataDictionary>();

            object metadataValue;
            Assert.True(part.ImportingProperty.Metadata.TryGetValue("ExportTypeIdentity", out metadataValue));
            Assert.IsType(typeof(string), metadataValue);
            Assert.Equal(typeof(PartWithExportMetadata).FullName, metadataValue);
        }

        #region Metaview filtering tests

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartOfObjectWithMetadataInterface), typeof(PartWithExportMetadataA), typeof(PartWithExportMetadataB))]
        public void ImportWithMetadataViewAsFilter(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPartOfObjectWithMetadataInterface>();

            // metadata "a" is mandatory per the interface, whereas "B" is optional.
            Assert.IsType<PartWithExportMetadataA>(importer.ImportingProperty.Value);
            Assert.Equal(null, importer.ImportingProperty.Metadata.SomeStringEnum);
            Assert.Equal(4, importer.ImportingProperty.Metadata.SomeInt);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartOfObjectWithMetadataInterface), typeof(PartWithExportMetadataSomeStringArray))]
        public void ImportWithMetadataViewAsFilterAndMetadatumWithStringArrayValue(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPartOfObjectWithMetadataInterface>();

            Assert.IsType<PartWithExportMetadataSomeStringArray>(importer.ImportingProperty.Value);
            Assert.Equal(new string[] { "alpha", "beta" }, importer.ImportingProperty.Metadata.SomeStringArray);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportingPartOfObjectWithMetadataInterface), typeof(PartWithExportMetadataSomeStringArray))]
        public void ImportWithMetadataViewAsFilterOfObjectArrayAndMetadatumWithStringArrayValue(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPartOfObjectWithMetadataInterface>();

            Assert.IsType<PartWithExportMetadataSomeStringArray>(importer.ImportingProperty.Value);
            Assert.Equal(new object[] { "alpha", "beta" }, importer.ImportingProperty.Metadata.SomeObjectArrayOfStrings);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportManyPartOfObjectWithMetadataInterface), typeof(PartWithExportMetadataA), typeof(PartWithExportMetadataB), typeof(PartWithExportMetadataAB))]
        public void ImportManyWithMetadataViewAsFilter(IContainer container)
        {
            var importer = container.GetExportedValue<ImportManyPartOfObjectWithMetadataInterface>();

            // metadata "a" is mandatory per the interface, whereas "B" is optional.
            Assert.Equal(2, importer.ImportingProperty.Count());
            Assert.Equal(1, importer.ImportingProperty.Select(v => v.Value).OfType<PartWithExportMetadataA>().Count());
            Assert.Equal(1, importer.ImportingProperty.Select(v => v.Value).OfType<PartWithExportMetadataAB>().Count());
        }

        [MefV1.Export]
        public class ImportingPartOfObjectWithMetadataInterface
        {
            [MefV1.Import("ExportWithMetadata")]
            public Lazy<object, IMetadata> ImportingProperty { get; set; }
        }

        [MefV1.Export]
        public class ImportManyPartOfObjectWithMetadataInterface
        {
            [MefV1.ImportMany("ExportWithMetadata")]
            public IEnumerable<Lazy<object, IMetadata>> ImportingProperty { get; set; }
        }

        [MefV1.Export("ExportWithMetadata", typeof(object))]
        [MefV1.ExportMetadata("a", "b")]
        public class PartWithExportMetadataA { }

        [MefV1.Export("ExportWithMetadata", typeof(object))]
        [MefV1.ExportMetadata("B", "c")]
        public class PartWithExportMetadataB { }

        [MefV1.Export("ExportWithMetadata", typeof(object))]
        [MefV1.ExportMetadata("a", "b")]
        [MefV1.ExportMetadata("B", "c")]
        public class PartWithExportMetadataAB { }

        [MefV1.Export("ExportWithMetadata", typeof(object))]
        [MefV1.ExportMetadata("a", "b")]
        [MefV1.ExportMetadata("SomeStringArray", new string[] { "alpha", "beta" })]
        [MefV1.ExportMetadata("SomeObjectArrayOfStrings", new object[] { "alpha", "beta" })]
        public class PartWithExportMetadataSomeStringArray { }

        #endregion

        #region MetadataViewWithMultipleValues test

        [MefFact(CompositionEngines.V1Compat, typeof(ExportWithMultipleMetadata), typeof(ImportOfMultipleMetadata))]
        public void MetadataViewWithMultipleValues(IContainer container)
        {
            var part = container.GetExportedValue<ImportOfMultipleMetadata>();
            IEnumerable<Type> metadataValue = part.ImportingProperty.Metadata.Name;
            Assert.Equal(2, metadataValue.Count());
            Assert.Contains(typeof(int), metadataValue);
            Assert.Contains(typeof(string), metadataValue);
        }

        public interface IMetadataViewForMultipleValues
        {
            IEnumerable<Type> Name { get; }
        }

        [MefV1.Export]
        [MefV1.ExportMetadata("Name", typeof(int), IsMultiple = true)]
        [MefV1.ExportMetadata("Name", typeof(string), IsMultiple = true)]
        public class ExportWithMultipleMetadata { }

        [MefV1.Export]
        public class ImportOfMultipleMetadata
        {
            [MefV1.Import]
            public Lazy<ExportWithMultipleMetadata, IMetadataViewForMultipleValues> ImportingProperty { get; set; }
        }

        #endregion

        #region MultipleExportMetadataTypedAppropriately test

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartImportingExportsWithMultipleTypedMetadata), typeof(PartWithMultipleTypedMetadata))]
        public void MultipleExportMetadataTypedAppropriately(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingExportsWithMultipleTypedMetadata>();
            object metadataValue = part.ImportingProperty.Metadata["Name"];
            Assert.IsType<string[]>(metadataValue);
            var array = (string[])metadataValue;
            Assert.Contains("hi", array);
            Assert.Equal(2, array.Where(v => v == null).Count());
        }

        [Export, ExportMetadata("Name", null), ExportMetadata("Name", "hi"), ExportMetadata("Name", null)]
        [MefV1.Export, MefV1.ExportMetadata("Name", null, IsMultiple = true), MefV1.ExportMetadata("Name", "hi", IsMultiple = true), MefV1.ExportMetadata("Name", null, IsMultiple = true)]
        public class PartWithMultipleTypedMetadata { }

        [Export]
        [MefV1.Export]
        public class PartImportingExportsWithMultipleTypedMetadata
        {
            [Import]
            [MefV1.Import]
            public Lazy<PartWithMultipleTypedMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        #endregion

        #region MultipleExportMetadataWithOnlyNullTyped test

        [MefFact(CompositionEngines.V1Compat, typeof(PartImportingExportsWithMultipleNullMetadata), typeof(PartWithMultipleNullMetadata))]
        public void MultipleExportMetadataWithOnlyNullTyped(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingExportsWithMultipleNullMetadata>();
            object dictionaryValue = part.ImportingPropertyWithDictionary.Metadata["Names"];
            IEnumerable<string> interfaceValue = part.ImportingPropertyWithInterface.Metadata.Names;
            Assert.Null(interfaceValue);
            Assert.Equal(new object[] { null, null }, dictionaryValue);
        }

        [Export, ExportMetadata("Names", null), ExportMetadata("Names", null)]
        [MefV1.Export, MefV1.ExportMetadata("Names", null, IsMultiple = true), MefV1.ExportMetadata("Names", null, IsMultiple = true)]
        public class PartWithMultipleNullMetadata { }

        public interface INamedMetadata
        {
            [DefaultValue(null)]
            IEnumerable<string> Names { get; }
        }

        [Export]
        [MefV1.Export]
        public class PartImportingExportsWithMultipleNullMetadata
        {
            [Import]
            [MefV1.Import]
            public Lazy<PartWithMultipleNullMetadata, INamedMetadata> ImportingPropertyWithInterface { get; set; }

            [Import]
            [MefV1.Import]
            public Lazy<PartWithMultipleNullMetadata, IDictionary<string, object>> ImportingPropertyWithDictionary { get; set; }
        }

        #endregion

        #region GetExports (plural) tests

        [MefFact(CompositionEngines.V1Compat, new Type[0])]
        [Trait("Container.GetExport", "Plural")]
        public void GetNamedExportsTMetadataEmpty(IContainer container)
        {
            IEnumerable<Lazy<object, IDictionary<string, object>>> result =
                container.GetExports<object, IDictionary<string, object>>("NoOneExportsThis");
            Assert.Equal(0, result.Count());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithExportMetadataA), typeof(PartWithExportMetadataB), typeof(PartWithExportMetadataAB))]
        [Trait("Container.GetExport", "Plural")]
        [Trait("Metadata", "TMetadata")]
        public void GetNamedExportsTMetadataInterface(IContainer container)
        {
            IEnumerable<Lazy<object, IMetadata>> result =
                container.GetExports<object, IMetadata>("ExportWithMetadata");
            Assert.Equal(2, result.Count());
            var a = result.Single(e => e.Metadata.a == "b" && e.Metadata.B == "someDefault");
            var ab = result.Single(e => e.Metadata.a == "b" && e.Metadata.B == "c");
            Assert.IsType<PartWithExportMetadataA>(a.Value);
            Assert.IsType<PartWithExportMetadataAB>(ab.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithExportMetadataA), typeof(PartWithExportMetadataB), typeof(PartWithExportMetadataAB))]
        [Trait("Container.GetExport", "Plural")]
        [Trait("Metadata", "TMetadata")]
        public void GetNamedExportsTMetadataClass(IContainer container)
        {
            IEnumerable<Lazy<object, MetadataClass>> result =
                container.GetExports<object, MetadataClass>("ExportWithMetadata");

            // Evidently MEF doesn't apply metadata view filters to exports when the metadata view is a class.
            Assert.Equal(3, result.Count());

            var aAndAB = result.Where(e => e.Metadata.a == "b");
            var b = result.Single(e => e.Metadata.a == null);
            aAndAB.Single(a => a.Value is PartWithExportMetadataA);
            aAndAB.Single(a => a.Value is PartWithExportMetadataAB);
            Assert.IsType<PartWithExportMetadataB>(b.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(FooExport1), typeof(FooExport2))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportsTMetadataEmpty(IContainer container)
        {
            IEnumerable<Lazy<object, IDictionary<string, object>>> result =
                container.GetExports<object, IDictionary<string, object>>();
            Assert.Equal(0, result.Count());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(FooExport1), typeof(FooExport2))]
        [Trait("Container.GetExport", "Plural")]
        [Trait("Metadata", "TMetadata")]
        public void GetExportsTMetadata(IContainer container)
        {
            IEnumerable<Lazy<IFoo, IMetadataBase>> result =
                container.GetExports<IFoo, IMetadataBase>();
            Assert.Equal(2, result.Count());

            var a = result.Single(e => e.Metadata.a == "1");
            var b = result.Single(e => e.Metadata.a == "2");

            Assert.IsType<FooExport1>(a.Value);
            Assert.IsType<FooExport2>(b.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(FooExport1))]
        [Trait("Container.GetExport", "Plural")]
        [Trait("Metadata", "TMetadata")]
        public void MetadataViewProxyHandlesObjectMethods(IContainer container)
        {
            var result = container.GetExports<IFoo, IMetadataBase>().First();
            Assert.True(result.Metadata.Equals(result.Metadata));
            result.Metadata.GetHashCode();
            Assert.NotNull(result.Metadata.GetType());
            Assert.NotNull(result.Metadata.ToString());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(FooExport1), typeof(FooExport2))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportsDictionaryMetadata(IContainer container)
        {
            IEnumerable<Lazy<IFoo, IDictionary<string, object>>> result =
                container.GetExports<IFoo, IDictionary<string, object>>();
            Assert.Equal(2, result.Count());

            var a = result.Single(e => (string)e.Metadata["a"] == "1");
            var b = result.Single(e => (string)e.Metadata["a"] == "2");

            Assert.IsType<FooExport1>(a.Value);
            Assert.IsType<FooExport2>(b.Value);
        }

        public interface IFoo { }

        [MefV1.Export(typeof(IFoo))]
        [MefV1.ExportMetadata("a", "1")]
        public class FooExport1 : IFoo { }

        [MefV1.Export(typeof(IFoo))]
        [MefV1.ExportMetadata("a", "2")]
        public class FooExport2 : IFoo { }

        #endregion

        #region Exported Method ExportTypeIdentity test

        [MefFact(CompositionEngines.V1Compat, typeof(MethodExportingPart), typeof(PartThatImportsMethod))]
        public void ExportedMethodHasExportTypeIdentityMetadata(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsMethod>();
            object metadataValue;
            Assert.True(part.ImportedMethod.Metadata.TryGetValue("ExportTypeIdentity", out metadataValue));
            Assert.IsType(typeof(string), metadataValue);
            Assert.Equal("System.Single(System.Int32,System.Int32)", metadataValue);
        }

        public class MethodExportingPart
        {
            [MefV1.Export]
            public float Add(int a, int b)
            {
                return a + b;
            }
        }

        [MefV1.Export]
        public class PartThatImportsMethod
        {
            [MefV1.Import]
            public Lazy<Func<int, int, float>, IDictionary<string, object>> ImportedMethod { get; set; }
        }

        #endregion

        #region Extreme values tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartImportingExtremeValues), typeof(PartWithExtremeValues))]
        public void ExportMetadataExtremeValues(IContainer container)
        {
            var part = container.GetExportedValue<PartImportingExtremeValues>();
            Assert.Equal(double.MaxValue, part.ImportingProperty.Metadata["doubleMaxValue"]);
            Assert.Equal(double.MinValue, part.ImportingProperty.Metadata["doubleMinValue"]);
            Assert.Equal(float.MaxValue, part.ImportingProperty.Metadata["floatMaxValue"]);
            Assert.Equal(float.MinValue, part.ImportingProperty.Metadata["floatMinValue"]);
        }

        [MefV1.Export, Export]
        [MefV1.ExportMetadata("doubleMaxValue", double.MaxValue), ExportMetadata("doubleMaxValue", double.MaxValue)]
        [MefV1.ExportMetadata("doubleMinValue", double.MinValue), ExportMetadata("doubleMinValue", double.MinValue)]
        [MefV1.ExportMetadata("floatMaxValue", float.MaxValue), ExportMetadata("floatMaxValue", float.MaxValue)]
        [MefV1.ExportMetadata("floatMinValue", float.MinValue), ExportMetadata("floatMinValue", float.MinValue)]
        public class PartWithExtremeValues { }

        [MefV1.Export, Export]
        public class PartImportingExtremeValues
        {
            [MefV1.Import, Import]
            public Lazy<PartWithExtremeValues, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        #endregion

        /// <summary>
        /// Documents MEFv1 behavior that the metadata view with default values improperly matches
        /// exports with incompatible metadata, and at runtime shows the metadata as null.
        /// </summary>
        [MefFact(CompositionEngines.V1, typeof(PartWithNamesSingleMetadata), typeof(PartImportingPartWithNamesSingleMetadata), NoCompatGoal = true)]
        public void ExportMetadataIsMultipleFalseIntoMultipleMetadataViewV1(IContainer container)
        {
            var export = container.GetExportedValue<PartImportingPartWithNamesSingleMetadata>();
            Assert.Equal(1, export.ImportingProperty.Count); // MEFv1 allows through the filter...
            Assert.Null(export.ImportingProperty[0].Metadata.Names); // ...but then fails to obtain the value.
        }

        /// <summary>
        /// Documents MEFv3 behavior as having a more accurate metadata view filter than MEFv1 in
        /// the same scenario as above.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1, typeof(PartWithNamesSingleMetadata), typeof(PartImportingPartWithNamesSingleMetadata))]
        public void ExportMetadataIsMultipleFalseIntoMultipleMetadataViewV3(IContainer container)
        {
            var export = container.GetExportedValue<PartImportingPartWithNamesSingleMetadata>();
            Assert.Equal(0, export.ImportingProperty.Count); // empty because the metadata filter is not satisfied (string != string[])
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("Names", "someName")]
        [Export]
        [ExportMetadata("Names", "someName")]
        public class PartWithNamesSingleMetadata { }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class PartImportingPartWithNamesSingleMetadata
        {
            [MefV1.ImportMany, ImportMany]
            public List<Lazy<PartWithNamesSingleMetadata, INamedMetadata>> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("a", "b")]
        [MefV1.ExportMetadata(nameof(AssemblyDiscoveryTests.IInternalMetadataView.MetadataOnInternalInterface), "internal!")]
        [Export]
        [ExportMetadata("a", "b")]
        [ExportMetadata(nameof(AssemblyDiscoveryTests.IInternalMetadataView.MetadataOnInternalInterface), "internal!")]
        public class PartWithExportMetadata { }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportingPartWithMetadataDictionary
        {
            [Import, MefV1.Import]
            public Lazy<PartWithExportMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportingPartWithMetadataInterface
        {
            [Import, MefV1.Import]
            public Lazy<PartWithExportMetadata, IMetadata> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportingPartWithMetadataClass
        {
            [Import, MefV1.Import]
            public Lazy<PartWithExportMetadata, MetadataClass> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportingPartWithNonPublicMetadataClass
        {
            [Import, MefV1.Import]
            internal Lazy<PartWithExportMetadata, NonPublicMetadataClass> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportManyPartWithMetadataDictionary
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<Lazy<PartWithExportMetadata, IDictionary<string, object>>> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportManyPartWithMetadataInterface
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<Lazy<PartWithExportMetadata, IMetadata>> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportManyPartWithInternalMetadataInterface
        {
            [ImportMany, MefV1.ImportMany]
            internal IEnumerable<Lazy<PartWithExportMetadata, IMetadataInternal>> ImportingProperty { get; set; }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class ImportManyPartWithMetadataClass
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<Lazy<PartWithExportMetadata, MetadataClass>> ImportingProperty { get; set; }
        }

        public interface IMetadataBase
        {
#pragma warning disable SA1300 // Element must begin with upper-case letter
            string a { get; }
#pragma warning restore SA1300 // Element must begin with upper-case letter
        }

        public interface IMetadata : IMetadataBase
        {
            [DefaultValue("someDefault")]
            string B { get; }

            [DefaultValue(null)]
            IEnumerable<string> SomeStringEnum { get; }

            [DefaultValue(null)]
            string[] SomeStringArray { get; }

            [DefaultValue(null)]
            object[] SomeObjectArrayOfStrings { get; }

            [DefaultValue(4)]
            int SomeInt { get; }
        }

        internal interface IMetadataInternal : IMetadata, AssemblyDiscoveryTests.IInternalMetadataView
        {
        }

        public class MetadataClass
        {
            // Only MEFv1 requires this constructor -- MEFv2 doesn't need it.
            public MetadataClass(IDictionary<string, object> data)
            {
                object value;
                if (data.TryGetValue("a", out value))
                {
                    this.a = (string)value;
                }
            }

#pragma warning disable SA1300 // Element must begin with upper-case letter
            public string a { get; set; }
#pragma warning restore SA1300 // Element must begin with upper-case letter
        }

        internal class NonPublicMetadataClass : MetadataClass
        {
            public NonPublicMetadataClass(IDictionary<string, object> data)
                : base(data)
            {
            }
        }

        public enum MetadataEnum
        {
            Value1,
            Value2
        }

        internal enum MetadataEnumNonPublic
        {
            Value1,
            Value2,
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("SomeName", MetadataEnum.Value2)]
        [MefV1.ExportMetadata("SomeName", MetadataEnum.Value2)]
        public class ExportWithEnumMetadata { }

        [Export]
        [MefV1.Export]
        [ExportMetadata("SomeName", MetadataEnumNonPublic.Value2)]
        [MefV1.ExportMetadata("SomeName", MetadataEnumNonPublic.Value2)]
        public class ExportWithNonPublicEnumMetadata { }

        [Export]
        [MefV1.Export]
        public class PartThatImportsEnumMetadata
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithEnumMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("SomeName", typeof(int))]
        [MefV1.ExportMetadata("SomeName", typeof(int))]
        public class ExportWithTypeMetadata { }

        [Export]
        [MefV1.Export]
        public class PartThatImportsTypeMetadata
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithTypeMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("SomeName", 'a')]
        [ExportMetadata("Apostrophe", '\'')]
        [MefV1.ExportMetadata("SomeName", 'a')]
        [MefV1.ExportMetadata("Apostrophe", '\'')]
        public class ExportWithCharMetadata { }

        [Export]
        [MefV1.Export]
        public class PartThatImportsCharMetadata
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithCharMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        [Export]
        [MefV1.Export]
        [ExportMetadata("SomeName", true)]
        [MefV1.ExportMetadata("SomeName", true)]
        public class ExportWithBoolMetadata { }

        [Export]
        [MefV1.Export]
        public class PartThatImportsBoolMetadata
        {
            [Import]
            [MefV1.Import]
            public Lazy<ExportWithBoolMetadata, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        #region Exhaustive metatadata value types testing

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartThatImportsPartWithExhaustiveMetadataValueTypes), typeof(PartWithExhaustiveMetadataValueTypes))]
        public void ExhaustiveMetadataValueTypes(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartThatImportsPartWithExhaustiveMetadataValueTypes>();
            var export = importingPart.ImportingProperty;

            // allowed types
            Assert.Equal(true, (bool)export.Metadata["bool"]);
            Assert.Equal(byte.MaxValue, (byte)export.Metadata["byte"]);
            Assert.Equal('a', (char)export.Metadata["char"]);
            Assert.Equal((double)5, (double)export.Metadata["double"]);
            Assert.Equal(double.MaxValue, (double)export.Metadata["doubleMax"]);
            Assert.Equal(double.MinValue, (double)export.Metadata["doubleMin"]);
            Assert.Equal((float)5, (float)export.Metadata["float"]);
            Assert.Equal(float.MaxValue, (float)export.Metadata["floatMax"]);
            Assert.Equal(float.MinValue, (float)export.Metadata["floatMin"]);
            Assert.Equal(int.MaxValue, (int)export.Metadata["int"]);
            Assert.Equal(long.MaxValue, (long)export.Metadata["long"]);
            Assert.Equal(sbyte.MaxValue, (sbyte)export.Metadata["sbyte"]);
            Assert.Equal(short.MaxValue, (short)export.Metadata["short"]);
            Assert.Equal("value", (string)export.Metadata["string"]);
            Assert.Equal(uint.MaxValue, (uint)export.Metadata["uint"]);
            Assert.Equal(ulong.MaxValue, (ulong)export.Metadata["ulong"]);
            Assert.Equal(ushort.MaxValue, (ushort)export.Metadata["ushort"]);
            Assert.Equal(typeof(string), (Type)export.Metadata["type"]);
            Assert.Equal(CreationPolicy.NonShared, (CreationPolicy)export.Metadata["enum"]);

            // arrays of allowed types
            Assert.Equal(new[] { true }, (bool[])export.Metadata["array_bool"]);
            Assert.Equal(new[] { byte.MaxValue }, (byte[])export.Metadata["array_byte"]);
            Assert.Equal(new[] { 'a' }, (char[])export.Metadata["array_char"]);
            Assert.Equal(new[] { (double)5 }, (double[])export.Metadata["array_double"]);
            Assert.Equal(new[] { double.MaxValue }, (double[])export.Metadata["array_doubleMax"]);
            Assert.Equal(new[] { double.MinValue }, (double[])export.Metadata["array_doubleMin"]);
            Assert.Equal(new[] { (float)5 }, (float[])export.Metadata["array_float"]);
            Assert.Equal(new[] { float.MaxValue }, (float[])export.Metadata["array_floatMax"]);
            Assert.Equal(new[] { float.MinValue }, (float[])export.Metadata["array_floatMin"]);
            Assert.Equal(new[] { int.MaxValue }, (int[])export.Metadata["array_int"]);
            Assert.Equal(new[] { long.MaxValue }, (long[])export.Metadata["array_long"]);
            Assert.Equal(new[] { sbyte.MaxValue }, (sbyte[])export.Metadata["array_sbyte"]);
            Assert.Equal(new[] { short.MaxValue }, (short[])export.Metadata["array_short"]);
            Assert.Equal(new[] { "value" }, (string[])export.Metadata["array_string"]);
            Assert.Equal(new[] { uint.MaxValue }, (uint[])export.Metadata["array_uint"]);
            Assert.Equal(new[] { ulong.MaxValue }, (ulong[])export.Metadata["array_ulong"]);
            Assert.Equal(new[] { ushort.MaxValue }, (ushort[])export.Metadata["array_ushort"]);
            Assert.Equal(new[] { typeof(string) }, (Type[])export.Metadata["array_type"]);
            Assert.Equal(new[] { CreationPolicy.NonShared }, (CreationPolicy[])export.Metadata["array_enum"]);
        }

        /// <summary>
        /// A MEF part with metadata of all types allowed by MEFv1.
        /// </summary>
        /// <remarks>
        /// System.ComponentModel.Composition limits metadata value types to just those found in
        /// the C# language specification 17.1.3.
        /// </remarks>
        [Export, MefV1.Export]
        //// allowed types
        [ExportMetadata("bool", true), MefV1.ExportMetadata("bool", true)]
        [ExportMetadata("byte", byte.MaxValue), MefV1.ExportMetadata("byte", byte.MaxValue)]
        [ExportMetadata("char", 'a'), MefV1.ExportMetadata("char", 'a')]
        [ExportMetadata("double", (double)5), MefV1.ExportMetadata("double", (double)5)]
        [ExportMetadata("doubleMax", double.MaxValue), MefV1.ExportMetadata("doubleMax", double.MaxValue)]
        [ExportMetadata("doubleMin", double.MinValue), MefV1.ExportMetadata("doubleMin", double.MinValue)]
        [ExportMetadata("float", (float)5), MefV1.ExportMetadata("float", (float)5)]
        [ExportMetadata("floatMax", float.MaxValue), MefV1.ExportMetadata("floatMax", float.MaxValue)]
        [ExportMetadata("floatMin", float.MinValue), MefV1.ExportMetadata("floatMin", float.MinValue)]
        [ExportMetadata("int", int.MaxValue), MefV1.ExportMetadata("int", int.MaxValue)]
        [ExportMetadata("long", long.MaxValue), MefV1.ExportMetadata("long", long.MaxValue)]
        [ExportMetadata("sbyte", sbyte.MaxValue), MefV1.ExportMetadata("sbyte", sbyte.MaxValue)]
        [ExportMetadata("short", short.MaxValue), MefV1.ExportMetadata("short", short.MaxValue)]
        [ExportMetadata("string", "value"), MefV1.ExportMetadata("string", "value")]
        [ExportMetadata("uint", uint.MaxValue), MefV1.ExportMetadata("uint", uint.MaxValue)]
        [ExportMetadata("ulong", ulong.MaxValue), MefV1.ExportMetadata("ulong", ulong.MaxValue)]
        [ExportMetadata("ushort", ushort.MaxValue), MefV1.ExportMetadata("ushort", ushort.MaxValue)]
        [ExportMetadata("type", typeof(string)), MefV1.ExportMetadata("type", typeof(string))]
        [ExportMetadata("enum", CreationPolicy.NonShared), MefV1.ExportMetadata("enum", CreationPolicy.NonShared)]
        //// arrays of allowed types
        [ExportMetadata("array_bool", new[] { true }), MefV1.ExportMetadata("array_bool", new[] { true })]
        [ExportMetadata("array_byte", new[] { byte.MaxValue }), MefV1.ExportMetadata("array_byte", new[] { byte.MaxValue })]
        [ExportMetadata("array_char", new[] { 'a' }), MefV1.ExportMetadata("array_char", new[] { 'a' })]
        [ExportMetadata("array_double", new[] { (double)5 }), MefV1.ExportMetadata("array_double", new[] { (double)5 })]
        [ExportMetadata("array_doubleMax", new[] { double.MaxValue }), MefV1.ExportMetadata("array_doubleMax", new[] { double.MaxValue })]
        [ExportMetadata("array_doubleMin", new[] { double.MinValue }), MefV1.ExportMetadata("array_doubleMin", new[] { double.MinValue })]
        [ExportMetadata("array_float", new[] { (float)5 }), MefV1.ExportMetadata("array_float", new[] { (float)5 })]
        [ExportMetadata("array_floatMax", new[] { float.MaxValue }), MefV1.ExportMetadata("array_floatMax", new[] { float.MaxValue })]
        [ExportMetadata("array_floatMin", new[] { float.MinValue }), MefV1.ExportMetadata("array_floatMin", new[] { float.MinValue })]
        [ExportMetadata("array_int", new[] { int.MaxValue }), MefV1.ExportMetadata("array_int", new[] { int.MaxValue })]
        [ExportMetadata("array_long", new[] { long.MaxValue }), MefV1.ExportMetadata("array_long", new[] { long.MaxValue })]
        [ExportMetadata("array_sbyte", new[] { sbyte.MaxValue }), MefV1.ExportMetadata("array_sbyte", new[] { sbyte.MaxValue })]
        [ExportMetadata("array_short", new[] { short.MaxValue }), MefV1.ExportMetadata("array_short", new[] { short.MaxValue })]
        [ExportMetadata("array_string", new[] { "value" }), MefV1.ExportMetadata("array_string", new[] { "value" })]
        [ExportMetadata("array_uint", new[] { uint.MaxValue }), MefV1.ExportMetadata("array_uint", new[] { uint.MaxValue })]
        [ExportMetadata("array_ulong", new[] { ulong.MaxValue }), MefV1.ExportMetadata("array_ulong", new[] { ulong.MaxValue })]
        [ExportMetadata("array_ushort", new[] { ushort.MaxValue }), MefV1.ExportMetadata("array_ushort", new[] { ushort.MaxValue })]
        [ExportMetadata("array_type", new[] { typeof(string) }), MefV1.ExportMetadata("array_type", new[] { typeof(string) })]
        [ExportMetadata("array_enum", new[] { CreationPolicy.NonShared }), MefV1.ExportMetadata("array_enum", new[] { CreationPolicy.NonShared })]
        public class PartWithExhaustiveMetadataValueTypes { }

        [Export, MefV1.Export]
        public class PartThatImportsPartWithExhaustiveMetadataValueTypes
        {
            [Import, MefV1.Import]
            public Lazy<PartWithExhaustiveMetadataValueTypes, IDictionary<string, object>> ImportingProperty { get; set; }
        }

        #endregion
    }
}
