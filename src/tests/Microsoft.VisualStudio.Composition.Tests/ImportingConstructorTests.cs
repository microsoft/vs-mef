// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            public RandomExport RandomExport { get; set; } = null!;
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
            Assert.Equal(default(NonPublicStruct), part.ConstructorArg);
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
                Assert.Equal(1, exports.Length);
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
                Assert.Equal(1, exports.Count);
                Assert.Equal("1", exports.First().Metadata.SomeMetadata);
                Assert.IsType<RandomExport>(exports.First().Value);
            }
        }

        public class FeatureMetadata
        {
            public string? SomeMetadata { get; private set; }

            public FeatureMetadata(IDictionary<string, object> data)
            {
                object? value;
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

        #region ImportingConstructor lazy import initialization, other part has importing property

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(RandomExport), typeof(PartWithImportingConstructorOfPartThatInitializesLater), typeof(PartThatInitializesLater))]
        public void ImportingConstructorWithLazyImportPartEventuallyInitializes(IContainer container)
        {
            PartWithImportingConstructorOfPartThatInitializesLater.EvaluateLazyInCtor = false;
            var root = container.GetExportedValue<PartWithImportingConstructorOfPartThatInitializesLater>();
            Assert.False(root.LaterPart.IsValueCreated); // this test means to verify the scenario of the lazy not evaluating till later.
            Assert.Same(root, root.LaterPart.Value.ImportingConstructorPart);
            Assert.NotNull(root.LaterPart.Value.RandomExport);
        }

        // V1 throws InvalidOperationException inside the ctor for this test, which if caught, turns into an InternalErrorException for this test.
        [MefFact(CompositionEngines.V1Compat, typeof(RandomExport), typeof(PartWithImportingConstructorOfPartThatInitializesLater), typeof(PartThatInitializesLater))]
        public void ImportingConstructorWithLazyImportPartEventuallyInitializesAfterEvaluatingInCtor(IContainer container)
        {
            PartWithImportingConstructorOfPartThatInitializesLater.EvaluateLazyInCtor = true;
            Assert.Throws<CompositionFailedException>(() =>
                container.GetExportedValue<PartWithImportingConstructorOfPartThatInitializesLater>());
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartThatInitializesLater
        {
            internal static bool EvaluateLazyInCtor;

            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartThatInitializesLater(Lazy<PartThatInitializesLater> laterPart)
            {
                this.LaterPart = laterPart;

                if (EvaluateLazyInCtor)
                {
                    Assert.Null(laterPart.Value.ImportingConstructorPart);
                }
            }

            public Lazy<PartThatInitializesLater> LaterPart { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatInitializesLater
        {
            [Import, MefV1.Import]
            public RandomExport RandomExport { get; set; } = null!;

            [Import, MefV1.Import]
            public PartWithImportingConstructorOfPartThatInitializesLater ImportingConstructorPart { get; set; } = null!;
        }

        #endregion

        #region ImportingConstructor lazy import initialization, other part has importing constructor also

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithImportingConstructorOfPartWithImportingConstructor), typeof(PartWithImportingConstructorOfPartWithLazyImportingConstructor))]
        public void ImportingConstructorWithLazyImportingConstructorPartEventuallyInitializes(IContainer container)
        {
            PartWithImportingConstructorOfPartWithImportingConstructor.EvaluateLazyInCtor = false;
            var root = container.GetExportedValue<PartWithImportingConstructorOfPartWithImportingConstructor>();
            Assert.Same(root, root.Import.Value.Other);
        }

        // V1 throws an InternalErrorException for this test.
        // V2 crashes with a StackOverflowException for this test.
        [MefFact(CompositionEngines.Unspecified, typeof(PartWithImportingConstructorOfPartWithImportingConstructor), typeof(PartWithImportingConstructorOfPartWithLazyImportingConstructor))]
        public void ImportingConstructorWithLazyImportingConstructorPartEventuallyInitializesAfterThrowingInCtor(IContainer container)
        {
            PartWithImportingConstructorOfPartWithImportingConstructor.EvaluateLazyInCtor = true;
            var root = container.GetExportedValue<PartWithImportingConstructorOfPartWithImportingConstructor>();
            Assert.Same(root, root.Import.Value.Other);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartWithImportingConstructor
        {
            internal static bool EvaluateLazyInCtor;

            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartWithImportingConstructor(Lazy<PartWithImportingConstructorOfPartWithLazyImportingConstructor> import)
            {
                if (EvaluateLazyInCtor)
                {
                    Assert.Throws<InvalidOperationException>(() => import.Value);
                }

                this.Import = import;
            }

            public Lazy<PartWithImportingConstructorOfPartWithLazyImportingConstructor> Import { get; private set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartWithLazyImportingConstructor
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartWithLazyImportingConstructor(PartWithImportingConstructorOfPartWithImportingConstructor other)
            {
                this.Other = other;
            }

            public PartWithImportingConstructorOfPartWithImportingConstructor Other { get; private set; }
        }

        #endregion

        #region ImportingConstructor imports another part that itself has an importing constructor that lazily imports the original

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void ImportingConstructorImportsOtherPartWithImportingConstructorWithLazyLoopBack(IContainer container)
        {
            var root = container.GetExportedValue<PartWithImportingConstructorImportingOtherPartWithLazyLoopBack>();
            Assert.Same(root, root.Other.Other.Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorImportingOtherPartWithLazyLoopBack
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorImportingOtherPartWithLazyLoopBack(PartWithImportingConstructorWithLazyLoopBack other)
            {
                this.Other = other;
            }

            public PartWithImportingConstructorWithLazyLoopBack Other { get; private set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorWithLazyLoopBack
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorWithLazyLoopBack(Lazy<PartWithImportingConstructorImportingOtherPartWithLazyLoopBack> other)
            {
                this.Other = other;
            }

            public Lazy<PartWithImportingConstructorImportingOtherPartWithLazyLoopBack> Other { get; private set; }
        }

        #endregion

        #region ImportingConstructor imports another part that has a lazy importing property pointing back

        // V2 fails to set the OtherLazy property to a non-null value.
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartWithImportingConstructorOfPartWithLazyLoopbackImportingProperty), typeof(PartWithLazyLoopbackImportingProperty))]
        public void ImportingConstructorOfPartWithLoopbackLazyImportingProperty(IContainer container)
        {
            var root = container.GetExportedValue<PartWithImportingConstructorOfPartWithLazyLoopbackImportingProperty>();
            Assert.NotNull(root.Other.OtherLazy);
            Assert.Equal(1, root.Other.OtherLazy.Length);
            Assert.Same(root, root.Other.OtherLazy[0].Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartWithLazyLoopbackImportingProperty
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartWithLazyLoopbackImportingProperty(PartWithLazyLoopbackImportingProperty other)
            {
                Assert.NotNull(other);
                this.Other = other;
            }

            public PartWithLazyLoopbackImportingProperty Other { get; private set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithLazyLoopbackImportingProperty
        {
            [ImportMany, MefV1.ImportMany]
            public Lazy<PartWithImportingConstructorOfPartWithLazyLoopbackImportingProperty>[] OtherLazy { get; private set; } = null!;
        }

        #endregion

        #region Query for importing constructor parts in various orders.

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithImportingConstructorOfLazyPartImportingThis1), typeof(PartWithImportingConstructorOfLazyPartImportingThis2), typeof(PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis))]
        public void QueryImportingPropertyPartFirst(IContainer container)
        {
            // Simply querying for C first makes both MEFv1 and MEFv2 happy to then get A and B later.
            container.GetExportedValue<PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis>();
            container.GetExportedValue<PartWithImportingConstructorOfLazyPartImportingThis1>();
            container.GetExportedValue<PartWithImportingConstructorOfLazyPartImportingThis2>();
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithImportingConstructorOfLazyPartImportingThis1), typeof(PartWithImportingConstructorOfLazyPartImportingThis2), typeof(PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis))]
        public void QueryImportingConstructorPartsEvaluateAfterOne(IContainer container)
        {
            // In testing V3 we specifically obtain A first so that Lazy<C>
            // goes through a transition of "now I should evaluate to a fully initialized C".
            var a = container.GetExportedValue<PartWithImportingConstructorOfLazyPartImportingThis1>();
            Assert.Same(a, a.C.Value.A);

            // Now get B, which should get its own Lazy<C> that does NOT require a fully initialized value.
            var b = container.GetExportedValue<PartWithImportingConstructorOfLazyPartImportingThis2>();
            Assert.Same(b, b.C.B);

            var c = container.GetExportedValue<PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis>();
            Assert.Same(a.C.Value, b.C);
            Assert.Same(c, b.C);
        }

        /// <summary>
        /// Verifies that MEF throws appropriately when querying for parts with importing
        /// constructors with Lazy imports of circular imports.
        /// </summary>
        /// <remarks>
        /// V1 throws an exception because Lazy imports to ImportingConstructors
        ///    must return fully initialized values and a circular dependency makes that impossible.
        /// V2 throws an InternalErrorException for this test.
        ///
        /// Although V1 and V2 fail this one, it's because neither can handle
        /// first querying for part with the importing constructor.
        /// But they *can* handle first querying for the part with the importing property.
        /// </remarks>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartWithImportingConstructorOfLazyPartImportingThis1), typeof(PartWithImportingConstructorOfLazyPartImportingThis2), typeof(PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis))]
        public void QueryImportingConstructorPartsEvaluateAfterTwo(IContainer container)
        {
            // In testing V3 we specifically obtain A first so that Lazy<C>
            // goes throw a transition of "now I should evaluate to a fully initialized C".
            var a = container.GetExportedValue<PartWithImportingConstructorOfLazyPartImportingThis1>();

            // Now get B, which should get its own Lazy<C> that does NOT require a fully initialized value.
            Assert.Throws<CompositionFailedException>(() =>
                container.GetExportedValue<PartWithImportingConstructorOfLazyPartImportingThis2>());
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfLazyPartImportingThis1
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfLazyPartImportingThis1(Lazy<PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis> c)
            {
                // Do NOT evaluate C because the idea is that C doesn't
                // initialize till B
                this.C = c;
            }

            public Lazy<PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis> C { get; private set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfLazyPartImportingThis2
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfLazyPartImportingThis2(Lazy<PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis> c)
            {
                Assert.Null(c.Value.B);
                this.C = c.Value;
            }

            public PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis C { get; private set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsTwoPartsWithImportingConstructorsOfLazyThis
        {
            [Import, MefV1.Import]
            public PartWithImportingConstructorOfLazyPartImportingThis1 A { get; set; } = null!;

            [Import, MefV1.Import]
            public PartWithImportingConstructorOfLazyPartImportingThis2 B { get; set; } = null!;
        }

        #endregion

        #region ImportingConstructor lazy import initialization, other part does NOT have a circular dependency

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithImportingConstructorThatImportsRandomExport), typeof(RandomExport))]
        public void ImportingConstructorCanEvaluateLazyImportWhereNoCircularDependencyExists(IContainer container)
        {
            var part = container.GetExportedValue<PartWithImportingConstructorThatImportsRandomExport>();
            Assert.NotNull(part);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorThatImportsRandomExport
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public PartWithImportingConstructorThatImportsRandomExport(Lazy<RandomExport> export)
            {
                Assert.NotNull(export.Value);
            }
        }

        #endregion
    }
}
