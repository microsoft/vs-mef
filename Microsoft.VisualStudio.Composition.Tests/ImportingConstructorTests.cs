namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class ImportingConstructorTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SimpleImportingConstructorPart), typeof(RandomExport))]
        public void SimpleImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<SimpleImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(LazyImportingConstructorPart), typeof(RandomExport))]
        public void LazyImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<LazyImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SpecialImportingConstructorPart), typeof(RandomExportWithContractName), typeof(RandomExport))]
        public void SpecialImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<SpecialImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(PrivateDefaultConstructorPart))]
        public void PrivateDefaultConstructor(IContainer container)
        {
            var part = container.GetExportedValue<PrivateDefaultConstructorPart>();
            Assert.NotNull(part);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(PrivateImportingConstructorPart), typeof(RandomExport))]
        public void PrivateImportingConstructor(IContainer container)
        {
            var part = container.GetExportedValue<PrivateImportingConstructorPart>();
            Assert.NotNull(part);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(PrivateImportingConstructorOpenGenericPart<,>), typeof(RandomExport))]
        public void PrivateImportingConstructorOpenGeneric(IContainer container)
        {
            var part = container.GetExportedValue<PrivateImportingConstructorOpenGenericPart<int, string>>();
            Assert.NotNull(part);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ImportingConstructorWithImportManyPart), typeof(RandomExport))]
        public void ImportingConstructorWithImportMany(IContainer container)
        {
            var part = container.GetExportedValue<ImportingConstructorWithImportManyPart>();
            Assert.Equal(1, part.ConstructorImports.Length);
            Assert.IsType<RandomExport>(part.ConstructorImports[0]);
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PrivateDefaultConstructorPart
        {
            private PrivateDefaultConstructorPart()
            {
            }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PrivateImportingConstructorPart
        {
            [MefV1.ImportingConstructor]
            private PrivateImportingConstructorPart(RandomExport export)
            {
                Assert.NotNull(export);
            }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PrivateImportingConstructorOpenGenericPart<T1, T2>
        {
            [MefV1.ImportingConstructor]
            private PrivateImportingConstructorOpenGenericPart(RandomExport export)
            {
                Assert.NotNull(export);
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class SimpleImportingConstructorPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public SimpleImportingConstructorPart(RandomExport export)
            {
                Assert.NotNull(export);
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class LazyImportingConstructorPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public LazyImportingConstructorPart(Lazy<RandomExport> export)
            {
                Assert.NotNull(export);
                Assert.NotNull(export.Value);
            }
        }

        #region ImportMany Lazy with metadata test

        [MefFact(CompositionEngines.V1Compat, typeof(RandomExport), typeof(ImportManyCollectionLazyWithMetadataConstructorPart), InvalidConfiguration = true)]
        public void ImportManyCollectionLazyWithMetadataConstructor(IContainer container)
        {
            var part = container.GetExportedValue<ImportManyCollectionLazyWithMetadataConstructorPart>();
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class ImportManyCollectionLazyWithMetadataConstructorPart
        {
            [MefV1.ImportingConstructor]
            public ImportManyCollectionLazyWithMetadataConstructorPart([MefV1.ImportMany] Collection<Lazy<IRandomExport, FeatureMetadata>> exports)
            {
                Assert.NotNull(exports);
                Assert.Equal(1, exports.Count());
                Assert.Equal("1", exports.First().Metadata.SomeMetadata);
                Assert.IsType<RandomExport>(exports.First().Value);
            }
        }

        public class FeatureMetadata
        {
            public string SomeMetadata { get; private set; }

            public FeatureMetadata(IDictionary<string, object> data)
            {
                object value;
                if (data.TryGetValue("SomeMetadata", out value))
                {
                    this.SomeMetadata = (string)value;
                }
            }
        }

        #endregion

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class SpecialImportingConstructorPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public SpecialImportingConstructorPart([Import("Special"), MefV1.Import("Special")] RandomExportWithContractName specialExport, RandomExport randomExport)
            {
                Assert.NotNull(specialExport);
                Assert.NotNull(randomExport);
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ImportingConstructorWithImportManyPart
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public ImportingConstructorWithImportManyPart([ImportMany, MefV1.ImportMany] RandomExport[] exports)
            {
                Assert.NotNull(exports);
                this.ConstructorImports = exports;
            }

            public RandomExport[] ConstructorImports { get; private set; }
        }

        [Export]
        [MefV1.Export, MefV1.Export(typeof(IRandomExport))]
        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [ExportMetadata("SomeMetadata", "1")]
        [MefV1.ExportMetadata("SomeMetadata", "1")]
        public class RandomExport : IRandomExport { }

        [Export("Special")]
        [MefV1.Export("Special"), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class RandomExportWithContractName { }

        // This type is intentionally internal to force specific code paths in code generation
        internal interface IRandomExport { }
    }
}
