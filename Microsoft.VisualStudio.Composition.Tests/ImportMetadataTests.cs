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

    public class ImportMetadataTests
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

        [MefFact(CompositionEngines.V2 | CompositionEngines.V1, typeof(ImportingPartWithMetadataDictionary), typeof(PartWithExportMetadata))]
        public void MetadataDictionaryInstanceSharedAcrossImports(IContainer container)
        {
            var importingPart1 = container.GetExportedValue<ImportingPartWithMetadataDictionary>();
            var importingPart2 = container.GetExportedValue<ImportingPartWithMetadataDictionary>();
            Assert.NotSame(importingPart1, importingPart2); // non-shared part is crucial to the integrity of this test.

            // Ensure that the dictionary instances are shared.
            Assert.Same(importingPart1.ImportingProperty.Metadata, importingPart2.ImportingProperty.Metadata);
        }

        [MefFact(CompositionEngines.V1, typeof(ImportingPartWithMetadataDictionary), typeof(PartWithExportMetadata))]
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

        [MefFact(CompositionEngines.Unspecified, typeof(ImportingPartWithMetadataInterface), typeof(PartWithExportMetadata))]
        public void MetadataViewInterfaceInstanceSharedAcrossImports(IContainer container)
        {
            var importingPart1 = container.GetExportedValue<ImportingPartWithMetadataInterface>();
            var importingPart2 = container.GetExportedValue<ImportingPartWithMetadataInterface>();
            Assert.NotSame(importingPart1, importingPart2); // non-shared part is crucial to the integrity of this test.

            // Ensure that the interface instances are shared.
            Assert.Same(importingPart1.ImportingProperty.Metadata, importingPart2.ImportingProperty.Metadata);
        }

        #region Metaview filtering tests

        [MefFact(CompositionEngines.V1, typeof(ImportingPartOfObjectWithMetadataInterface), typeof(PartWithExportMetadataA), typeof(PartWithExportMetadataB))]
        public void ImportWithMetadataViewAsFilter(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingPartOfObjectWithMetadataInterface>();

            // metadata "a" is mandatory per the interface, whereas "B" is optional.
            Assert.IsType<PartWithExportMetadataA>(importer.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1, typeof(ImportManyPartOfObjectWithMetadataInterface), typeof(PartWithExportMetadataA), typeof(PartWithExportMetadataB), typeof(PartWithExportMetadataAB))]
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

        #endregion

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("a", "b")]
        [Export]
        [ExportMetadata("a", "b")]
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
        public class ImportManyPartWithMetadataClass
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<Lazy<PartWithExportMetadata, MetadataClass>> ImportingProperty { get; set; }
        }

        public interface IMetadata
        {
            string a { get; }

            [DefaultValue("someDefault")]
            string B { get; }
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

            public string a { get; set; }
        }
    }
}
