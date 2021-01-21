// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("ExportFactory", "")]
    public class ExportFactoryTests
    {
        public ExportFactoryTests()
        {
            NonSharedPart.InstantiationCounter = 0;
            NonSharedPart.DisposalCounter = 0;
        }

        #region V1 tests

        [Trait("Disposal", "")]
        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(NonSharedPart))]
        public void ExportFactoryForNonSharedPartCreationDisposalV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            Assert.NotNull(partFactory.Factory);
            Assert.NotNull(partFactory.FactoryWithMetadata);
            Assert.Equal("V", partFactory.FactoryWithMetadata.Metadata["N"]);
            Assert.Equal("V", partFactory.FactoryWithTMetadata.Metadata.N);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(1, NonSharedPart.DisposalCounter);

            container.Dispose();
            Assert.Equal(1, NonSharedPart.DisposalCounter);
        }

        [Trait("Disposal", "")]
        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(PartWithPropertyExportingNonSharedPart))]
        public void ExportFactoryForNonSharedPartCreationDisposalFromPropertyV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            Assert.NotNull(partFactory.Factory);
            Assert.NotNull(partFactory.FactoryWithMetadata);
            Assert.Equal("V", partFactory.FactoryWithMetadata.Metadata["N"]);
            Assert.Equal("V", partFactory.FactoryWithTMetadata.Metadata.N);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(0, NonSharedPart.DisposalCounter);

            container.Dispose();
            Assert.Equal(0, NonSharedPart.DisposalCounter);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(NonSharedPart))]
        public void ExportFactoryForNonSharedPartInstantiatesMultiplePartsV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            var value1 = partFactory.Factory.CreateExport().Value;
            var value2 = partFactory.Factory.CreateExport().Value;
            Assert.NotSame(value1, value2);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1WithExplicitContractType), typeof(NonSharedPart))]
        public void ExportFactoryWithExplicitContractTypeV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1WithExplicitContractType>();
            Assert.NotNull(partFactory.Factory);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(1, NonSharedPart.DisposalCounter);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryManyV1), typeof(NonSharedPart), typeof(NonSharedPart2))]
        public void ExportFactoryForNonSharedPartManyV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryManyV1>();
            Assert.NotNull(partFactory.Factories);
            Assert.Equal(2, partFactory.Factories.Count());

            Assert.NotNull(partFactory.FactoriesWithMetadata);
            Assert.Equal(2, partFactory.FactoriesWithMetadata.Count());
            var factory1 = partFactory.FactoriesWithMetadata.Single(f => "V".Equals(f.Metadata["N"]));
            var factory2 = partFactory.FactoriesWithMetadata.Single(f => "V2".Equals(f.Metadata["N"]));

            using (var exportContext = factory1.CreateExport())
            {
                Assert.IsType<NonSharedPart>(exportContext.Value);
            }

            using (var exportContext = factory2.CreateExport())
            {
                Assert.IsType<NonSharedPart2>(exportContext.Value);
            }
        }

        /// <summary>
        /// Verifies a very tricky combination of export factories, explicit contract types and open generic exports.
        /// </summary>
        /// <remarks>
        /// CPS did this in Dev12 with MEFv1. I don't know why it doesn't work in this unit test (or in a console app I wrote) against MEFv1.
        /// But somehow it worked in VS. Perhaps due to some nuance in the ExportProviders CPS set up.
        /// </remarks>
        [MefFact(CompositionEngines.V3EmulatingV1, typeof(PartFactoryOfOpenGenericPart), typeof(NonSharedOpenGenericExportPart<>))]
        [Trait("GenericExports", "Open")]
        public void ExportFactoryWithOpenGenericExport(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryOfOpenGenericPart>();
            Assert.NotNull(partFactory.Factory);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.IsType<NonSharedOpenGenericExportPart<IDisposable>>(value);
            }
        }

        [MefV1.Export]
        public class PartFactoryV1WithExplicitContractType
        {
            [MefV1.Import(typeof(NonSharedPart))]
            public MefV1.ExportFactory<IDisposable> Factory { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartFactoryV1
        {
            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart> Factory { get; set; } = null!;

            [MefV1.Import(AllowDefault = true)]
            public MefV1.ExportFactory<NonSharedPartThatImportsAnotherNonSharedPart> TransitiveNonSharedFactory { get; set; } = null!;

            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart, IDictionary<string, object>> FactoryWithMetadata { get; set; } = null!;

            [MefV1.Import]
            public MefV1.ExportFactory<NonSharedPart, IMetadata> FactoryWithTMetadata { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartFactoryManyV1
        {
            [MefV1.ImportMany]
            public IEnumerable<MefV1.ExportFactory<NonSharedPart>> Factories { get; set; } = null!;

            [MefV1.ImportMany]
            public IEnumerable<MefV1.ExportFactory<NonSharedPart, IDictionary<string, object>>> FactoriesWithMetadata { get; set; } = null!;
        }

        public interface INonSharedOpenGenericExportPart<T> { }

        [MefV1.Export(typeof(NonSharedOpenGenericExportPart<>))]
        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedOpenGenericExportPart<T> : INonSharedOpenGenericExportPart<T>
        {
        }

        [MefV1.Export]
        public class PartFactoryOfOpenGenericPart
        {
            [MefV1.Import(typeof(NonSharedOpenGenericExportPart<IDisposable>))]
            public MefV1.ExportFactory<INonSharedOpenGenericExportPart<IDisposable>> Factory { get; set; } = null!;
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicFactoryOfInternalPartViaPublicInterface), typeof(InternalPart))]
        public void ExportFactoryForInternalPartViaPublicInterface(IContainer container)
        {
            var factory = container.GetExportedValue<PublicFactoryOfInternalPartViaPublicInterface>();
            var export = factory.InternalPartFactory.CreateExport();
            Assert.NotNull(export.Value);
            Assert.IsType<InternalPart>(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonPublicFactoryOfInternalPart), typeof(InternalPart))]
        public void ExportFactoryForInternalPart(IContainer container)
        {
            var factory = container.GetExportedValue<NonPublicFactoryOfInternalPart>();
            var export = factory.InternalPartFactory.CreateExport();
            Assert.NotNull(export.Value);
            Assert.IsType<InternalPart>(export.Value);
        }

        [MefV1.Export]
        public class PublicFactoryOfInternalPartViaPublicInterface
        {
            [MefV1.Import]
            public MefV1.ExportFactory<IDisposable> InternalPartFactory { get; set; } = null!;
        }

        [MefV1.Export]
        public class NonPublicFactoryOfInternalPart
        {
            [MefV1.Import]
            internal MefV1.ExportFactory<InternalPart> InternalPartFactory { get; set; } = null!;
        }

        [MefV1.Export(typeof(IDisposable))]
        [MefV1.Export]
        internal class InternalPart : IDisposable
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region V2 tests

        [Trait("Disposal", "")]
        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(NonSharedPart))]
        public void ExportFactoryForNonSharedPartCreationDisposalV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            Assert.NotNull(partFactory.Factory);
            Assert.NotNull(partFactory.FactoryWithMetadata);
            Assert.Equal("V", partFactory.FactoryWithMetadata.Metadata["N"]);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(1, NonSharedPart.DisposalCounter);

            container.Dispose();
            Assert.Equal(1, NonSharedPart.DisposalCounter);
        }

        [Trait("Disposal", "")]
        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(PartWithPropertyExportingNonSharedPart))]
        public void ExportFactoryForNonSharedPartCreationDisposalFromPropertyV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            Assert.NotNull(partFactory.Factory);
            Assert.NotNull(partFactory.FactoryWithMetadata);
            Assert.Equal("V", partFactory.FactoryWithMetadata.Metadata["N"]);
            using (var exportContext = partFactory.Factory.CreateExport())
            {
                Assert.NotNull(exportContext);
                Assert.Equal(1, NonSharedPart.InstantiationCounter);

                var value = exportContext.Value;
                Assert.NotNull(value);
                Assert.Equal(0, NonSharedPart.DisposalCounter);
            }

            Assert.Equal(0, NonSharedPart.DisposalCounter);

            container.Dispose();
            Assert.Equal(0, NonSharedPart.DisposalCounter);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(NonSharedPart))]
        public void ExportFactoryForNonSharedPartInstantiatesMultiplePartsV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            var value1 = partFactory.Factory.CreateExport().Value;
            var value2 = partFactory.Factory.CreateExport().Value;
            Assert.NotSame(value1, value2);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(NonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void ExportFactoryForNonSharedPartNoLeakAfterExportDisposalV2(IContainer container)
        {
            WeakReference exportedPart = ExportFactoryForNonSharedPartNoLeakAfterExportDisposalV2_Helper(container);
            GC.Collect();
            Assert.False(exportedPart.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ExportFactoryForNonSharedPartNoLeakAfterExportDisposalV2_Helper(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            var export = partFactory.Factory.CreateExport();
            WeakReference exportedValue = new WeakReference(export.Value);

            // This should remove the exported part from the container.
            export.Dispose();
            Assert.True(export.Value.Disposed);
            return exportedValue;
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(NonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void ExportFactoryForNonSharedPartNoLeakAfterExportDisposal_TransitiveV2(IContainer container)
        {
            (WeakReference part1, WeakReference part2) = ExportFactoryForNonSharedPartNoLeakAfterExportDisposal_TransitiveV2_Helper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
            Assert.False(part2.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Direct, WeakReference Transitive) ExportFactoryForNonSharedPartNoLeakAfterExportDisposal_TransitiveV2_Helper(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            var export = partFactory.TransitiveNonSharedFactory.CreateExport();
            Assert.NotNull(export.Value.NonSharedPart);
            WeakReference exportedValue = new WeakReference(export.Value);
            WeakReference transitiveExportedValue = new WeakReference(export.Value.NonSharedPart);

            // This should remove the exported part from the container.
            export.Dispose();
            return (exportedValue, transitiveExportedValue);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryV2), typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(NonSharedPart))]
        [Trait("Disposal", "")]
        public void ExportFactoryForNonSharedPartDisposedAfterExportDisposal_TransitiveV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV2>();
            var export = partFactory.TransitiveNonSharedFactory.CreateExport();
            Assert.NotNull(export.Value.NonSharedPart);

            export.Dispose();
            Assert.True(export.Value.Disposed);
            Assert.True(export.Value.NonSharedPart.Disposed);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(NonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void ExportFactoryForNonSharedPartNoLeakAfterExportDisposalV1(IContainer container)
        {
            WeakReference exportedPart = ExportFactoryForNonSharedPartNoLeakAfterExportDisposalV1_Helper(container);
            GC.Collect();
            Assert.False(exportedPart.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ExportFactoryForNonSharedPartNoLeakAfterExportDisposalV1_Helper(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            var export = partFactory.Factory.CreateExport();
            WeakReference exportedValue = new WeakReference(export.Value);

            // This should remove the exported part from the container.
            export.Dispose();
            Assert.True(export.Value.Disposed);
            return exportedValue;
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(NonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void ExportFactoryForNonSharedPartNoLeakAfterExportDisposal_TransitiveV1(IContainer container)
        {
            (WeakReference part1, WeakReference part2) = ExportFactoryForNonSharedPartNoLeakAfterExportDisposal_TransitiveV1_Helper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
            Assert.False(part2.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Direct, WeakReference Transitive) ExportFactoryForNonSharedPartNoLeakAfterExportDisposal_TransitiveV1_Helper(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            var export = partFactory.TransitiveNonSharedFactory.CreateExport();
            Assert.NotNull(export.Value.NonSharedPart);
            WeakReference exportedValue = new WeakReference(export.Value);
            WeakReference transitiveExportedValue = new WeakReference(export.Value.NonSharedPart);

            // This should remove the exported part from the container.
            export.Dispose();
            return (exportedValue, transitiveExportedValue);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartFactoryV1), typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(NonSharedPart))]
        [Trait("Disposal", "")]
        public void ExportFactoryForNonSharedPartDisposedAfterExportDisposal_TransitiveV1(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryV1>();
            var export = partFactory.TransitiveNonSharedFactory.CreateExport();
            Assert.NotNull(export.Value.NonSharedPart);

            export.Dispose();
            Assert.True(export.Value.Disposed);
            Assert.True(export.Value.NonSharedPart.Disposed);
        }

        [MefFact(CompositionEngines.V2Compat, typeof(PartFactoryManyV2), typeof(NonSharedPart), typeof(NonSharedPart2))]
        public void ExportFactoryForNonSharedPartManyV2(IContainer container)
        {
            var partFactory = container.GetExportedValue<PartFactoryManyV2>();
            Assert.NotNull(partFactory.Factories);
            Assert.Equal(2, partFactory.Factories.Count());

            Assert.NotNull(partFactory.FactoriesWithMetadata);
            Assert.Equal(2, partFactory.FactoriesWithMetadata.Count());
            var factory1 = partFactory.FactoriesWithMetadata.Single(f => "V".Equals(f.Metadata["N"]));
            var factory2 = partFactory.FactoriesWithMetadata.Single(f => "V2".Equals(f.Metadata["N"]));

            using (var exportContext = factory1.CreateExport())
            {
                Assert.IsType<NonSharedPart>(exportContext.Value);
            }

            using (var exportContext = factory2.CreateExport())
            {
                Assert.IsType<NonSharedPart2>(exportContext.Value);
            }
        }

        [Export]
        public class PartFactoryV2
        {
            [Import]
            public ExportFactory<NonSharedPart> Factory { get; set; } = null!;

            [Import(AllowDefault = true)]
            public ExportFactory<NonSharedPartThatImportsAnotherNonSharedPart> TransitiveNonSharedFactory { get; set; } = null!;

            [Import]
            public ExportFactory<NonSharedPart, IDictionary<string, object>> FactoryWithMetadata { get; set; } = null!;
        }

        [Export]
        public class PartFactoryManyV2
        {
            [ImportMany]
            public IEnumerable<ExportFactory<NonSharedPart>> Factories { get; set; } = null!;

            [ImportMany]
            public IEnumerable<ExportFactory<NonSharedPart, IDictionary<string, object>>> FactoriesWithMetadata { get; set; } = null!;
        }

        #endregion

        #region Invalid configuration tests

        [MefFact(CompositionEngines.V1Compat, typeof(ExportWithSharedCreationPolicy), typeof(ExportFactoryOfSharedPartV1Part), InvalidConfiguration = true)]
        public void ExportFactoryOfSharedPartV1(IContainer container)
        {
            container.GetExportedValue<ExportFactoryOfSharedPartV1Part>();
        }

        [MefFact(CompositionEngines.V2, typeof(ExportWithSharedCreationPolicy), typeof(ExportFactoryOfSharedPartV2Part), NoCompatGoal = true)]
        public void ExportFactoryOfSharedPartV2(IContainer container)
        {
            // In V2, ExportFactory around a shared part is actually legal (oddly), and produces the *same* shared value repeatedly.
            var factory = container.GetExportedValue<ExportFactoryOfSharedPartV2Part>();
            var value1 = factory.Factory.CreateExport().Value;
            var value2 = factory.Factory.CreateExport().Value;
            Assert.Same(value1, value2);
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.Shared)]
        [Export, Shared]
        public class ExportWithSharedCreationPolicy { }

        [MefV1.Export]
        public class ExportFactoryOfSharedPartV1Part
        {
            [MefV1.Import]
            public MefV1.ExportFactory<ExportWithSharedCreationPolicy> Factory { get; set; } = null!;
        }

        [Export]
        public class ExportFactoryOfSharedPartV2Part
        {
            [Import]
            public ExportFactory<ExportWithSharedCreationPolicy> Factory { get; set; } = null!;
        }

        #endregion

        #region ExportFactory with CreationPolicy == Any

        [MefFact(CompositionEngines.V1Compat, typeof(ExportWithAnyCreationPolicy), typeof(ExportFactoryOfAnyCreationPolicyPartV1Part))]
        public void ExportFactoryOfAnyCreationPolicyPartV1(IContainer container)
        {
            var factory = container.GetExportedValue<ExportFactoryOfAnyCreationPolicyPartV1Part>();
            var value1 = factory.Factory.CreateExport().Value;
            var value2 = factory.Factory.CreateExport().Value;
            Assert.NotSame(value1, value2);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportWithAnyCreationPolicy), typeof(ExportFactoryAndImportOfAnyCreationPolicyPartV1), typeof(ImportOfAnyCreationPolicyPartV1))]
        public void AnyCreationPolicyPartCreatedViaExportFactroyAndAsRegularImport(IContainer container)
        {
            var factory = container.GetExportedValue<ExportFactoryAndImportOfAnyCreationPolicyPartV1>();
            var importer = container.GetExportedValue<ImportOfAnyCreationPolicyPartV1>();
            Assert.NotNull(importer.SharedImport);
            Assert.Same(factory.SharedImport, importer.SharedImport);

            var value1 = factory.Factory.CreateExport().Value;
            var value2 = factory.Factory.CreateExport().Value;
            Assert.NotSame(value1, value2);
        }

        [MefV1.Export]
        public class ExportWithAnyCreationPolicy { }

        [MefV1.Export]
        public class ExportFactoryOfAnyCreationPolicyPartV1Part
        {
            [MefV1.Import]
            public MefV1.ExportFactory<ExportWithAnyCreationPolicy> Factory { get; set; } = null!;
        }

        [MefV1.Export]
        public class ExportFactoryAndImportOfAnyCreationPolicyPartV1 : ExportFactoryOfAnyCreationPolicyPartV1Part
        {
            [MefV1.Import]
            public ExportWithAnyCreationPolicy SharedImport { get; set; } = null!;
        }

        [MefV1.Export]
        public class ImportOfAnyCreationPolicyPartV1
        {
            [MefV1.Import]
            public ExportWithAnyCreationPolicy SharedImport { get; set; } = null!;
        }

        #endregion

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("N", "V")]
        [Export]
        [ExportMetadata("N", "V")]
        public class NonSharedPart : IDisposable
        {
            internal static int InstantiationCounter;
            internal static int DisposalCounter;

            public NonSharedPart()
            {
                InstantiationCounter++;
            }

            internal bool Disposed { get; private set; }

            public void Dispose()
            {
                DisposalCounter++;
                this.Disposed = true;
            }
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class NonSharedPartThatImportsAnotherNonSharedPart : IDisposable
        {
            internal static int InstantiationCounter;
            internal static int DisposalCounter;

            public NonSharedPartThatImportsAnotherNonSharedPart()
            {
                InstantiationCounter++;
            }

            [Import, MefV1.Import]
            public NonSharedPart NonSharedPart { get; set; } = null!;

            internal bool Disposed { get; private set; }

            public void Dispose()
            {
                DisposalCounter++;
                this.Disposed = true;
            }
        }

        [MefV1.Export(typeof(NonSharedPart)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [MefV1.ExportMetadata("N", "V2")]
        [Export(typeof(NonSharedPart))]
        [ExportMetadata("N", "V2")]
        public class NonSharedPart2 : NonSharedPart
        {
        }

        public class PartWithPropertyExportingNonSharedPart
        {
            [MefV1.Export, Export]
            [MefV1.ExportMetadata("N", "V")]
            [ExportMetadata("N", "V")]
            public NonSharedPart ExportingProperty => new NonSharedPart();
        }

        public interface IMetadata
        {
            string N { get; }
        }
    }
}
