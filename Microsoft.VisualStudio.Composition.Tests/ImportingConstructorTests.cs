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

        #region ImportingConstructorImportsAreFullyInitialized test

        /// <summary>
        /// Verifies that ImportingConstructor's imports are satisfied by exports from parts that
        /// are themselves fully initialized.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartThatImportsPartWithOwnImports), typeof(PartThatImportsRandomExport), typeof(RandomExport))]
        public void ImportingConstructorImportsAreFullyInitialized(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsPartWithOwnImports>();
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsPartWithOwnImports
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartThatImportsPartWithOwnImports(PartThatImportsRandomExport export)
            {
                Assert.NotNull(export.RandomExport);
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsRandomExport
        {
            [Import, MefV1.Import]
            public RandomExport RandomExport { get; set; }
        }

        #endregion

        #region AllowDefault tests

        [Trait("AllowDefault", "true")]
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithAllowDefaultImportingConstructor))]
        public void ImportingConstructorWithAllowDefaultAndNoExport(IContainer container)
        {
            var part = container.GetExportedValue<PartWithAllowDefaultImportingConstructor>();
            Assert.Null(part.ConstructorArg);
        }

        [Trait("AllowDefault", "true")]
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithAllowDefaultStructImportingConstructor))]
        public void ImportingConstructorWithAllowDefaultStructAndNoExport(IContainer container)
        {
            var part = container.GetExportedValue<PartWithAllowDefaultStructImportingConstructor>();
            Assert.Equal(0, part.ConstructorArg);
        }

        [Trait("AllowDefault", "true")]
        [MefFact(CompositionEngines.V1Compat, typeof(PartWithAllowDefaultNonPublicStructImportingConstructor))]
        public void ImportingConstructorWithAllowDefaultNonPublicStructAndNoExport(IContainer container)
        {
            var part = container.GetExportedValue<PartWithAllowDefaultNonPublicStructImportingConstructor>();
            Assert.Equal(new NonPublicStruct(), part.ConstructorArg);
        }

        [Trait("AllowDefault", "true")]
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithAllowDefaultImportingConstructor), typeof(RandomExport))]
        public void ImportingConstructorWithAllowDefaultAndAnExport(IContainer container)
        {
            var part = container.GetExportedValue<PartWithAllowDefaultImportingConstructor>();
            Assert.NotNull(part.ConstructorArg);
        }

        [Export, MefV1.Export]
        public class PartWithAllowDefaultImportingConstructor
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithAllowDefaultImportingConstructor([Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]RandomExport export)
            {
                this.ConstructorArg = export;
            }

            public RandomExport ConstructorArg { get; set; }
        }

        [Export, MefV1.Export]
        public class PartWithAllowDefaultStructImportingConstructor
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithAllowDefaultStructImportingConstructor([Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]int export)
            {
                this.ConstructorArg = export;
            }

            public int ConstructorArg { get; set; }
        }

        [MefV1.Export]
        public class PartWithAllowDefaultNonPublicStructImportingConstructor
        {
            [MefV1.ImportingConstructor]
            internal PartWithAllowDefaultNonPublicStructImportingConstructor([MefV1.Import(AllowDefault = true)]NonPublicStruct export)
            {
                this.ConstructorArg = export;
            }

            internal NonPublicStruct ConstructorArg { get; set; }
        }

        #endregion

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

        [MefFact(CompositionEngines.V1Compat, typeof(RandomExport), typeof(ImportManyEnumerableLazyWithMetadataConstructorPart))]
        public void ImportManyEnumerableLazyWithMetadataConstructor(IContainer container)
        {
            var part = container.GetExportedValue<ImportManyEnumerableLazyWithMetadataConstructorPart>();
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class ImportManyEnumerableLazyWithMetadataConstructorPart
        {
            [MefV1.ImportingConstructor]
            public ImportManyEnumerableLazyWithMetadataConstructorPart([MefV1.ImportMany] IEnumerable<Lazy<IRandomExport, FeatureMetadata>> exports)
            {
                Assert.NotNull(exports);
                Assert.Equal(1, exports.Count());
                Assert.Equal("1", exports.First().Metadata.SomeMetadata);
                Assert.IsType<RandomExport>(exports.First().Value);
            }
        }

        [MefFact(CompositionEngines.V1Compat, typeof(RandomExport), typeof(ImportManyArrayLazyWithMetadataConstructorPart))]
        public void ImportManyArrayLazyWithMetadataConstructor(IContainer container)
        {
            var part = container.GetExportedValue<ImportManyArrayLazyWithMetadataConstructorPart>();
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class ImportManyArrayLazyWithMetadataConstructorPart
        {
            [MefV1.ImportingConstructor]
            public ImportManyArrayLazyWithMetadataConstructorPart([MefV1.ImportMany] Lazy<IRandomExport, FeatureMetadata>[] exports)
            {
                Assert.NotNull(exports);
                Assert.Equal(1, exports.Count());
                Assert.Equal("1", exports.First().Metadata.SomeMetadata);
                Assert.IsType<RandomExport>(exports.First().Value);
            }
        }

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

        internal struct NonPublicStruct { }

        #region ImportingConstructor lazy import initialization

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(RandomExport), typeof(PartWithImportingConstructorOfPartThatInitializesLater), typeof(PartThatInitializesLater))]
        public void ImportingConstructorWithLazyImportPartEventuallyInitializes(IContainer container)
        {
            var root = container.GetExportedValue<PartWithImportingConstructorOfPartThatInitializesLater>();
            Assert.Same(root, root.LaterPart.Value.ImportingConstructorPart);
            Assert.NotNull(root.LaterPart.Value.RandomExport);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartThatInitializesLater
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartThatInitializesLater(Lazy<PartThatInitializesLater> laterPart)
            {
                this.LaterPart = laterPart;
            }

            public Lazy<PartThatInitializesLater> LaterPart { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatInitializesLater
        {
            [Import, MefV1.Import]
            public RandomExport RandomExport { get; set; }

            [Import, MefV1.Import]
            public PartWithImportingConstructorOfPartThatInitializesLater ImportingConstructorPart { get; set; }
        }

        #endregion
    }
}
