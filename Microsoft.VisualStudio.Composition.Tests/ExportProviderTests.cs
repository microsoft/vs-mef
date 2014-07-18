namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class ExportProviderTests
    {
        [MefFact(CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1, typeof(PartThatImportsExportProvider), typeof(SomeOtherPart))]
        public void GetExportsNonGeneric(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            var exportProvider = importer.ExportProvider;

            var importDefinition = new ImportDefinition(
                typeof(SomeOtherPart).FullName,
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary<string, object>.Empty,
                ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty);
            IEnumerable<Export> exports = exportProvider.GetExports(importDefinition);
            var otherPart2 = exports.Single().Value;
            Assert.NotNull(otherPart2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExportWithMetadataDictionary(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, IDictionary<string, object>>();
            Assert.Equal(1, export.Metadata["A"]);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExportWithMetadataView(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, SomeOtherPartMetadataView>();
            Assert.Equal(1, export.Metadata.A);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1/*Compat | CompositionEngines.V3EmulatingV2*/, typeof(SomeOtherPart))]
        public void GetExportWithFilteringMetadataView(IContainer container)
        {
            var exports = container.GetExports<SomeOtherPart, MetadataViewWithBMember>();
            Assert.Equal(0, exports.Count());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(Apple))]
        public void GetExportOfTypeByObjectAndContractName(IContainer container)
        {
            var apple = container.GetExportedValue<object>("SomeContract");
            Assert.IsType(typeof(Apple), apple);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Apple))]
        public void GetExportOfTypeByBaseTypeAndContractName(IContainer container)
        {
            var apples = container.GetExportedValues<Fruit>("SomeContract");
            Assert.Equal(0, apples.Count());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(SomeOtherPart))]
        public void GetExportedValueOfExportFactoryOfT(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<ExportFactory<SomeOtherPart>>());
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsExportProvider
        {
            [Import, MefV1.Import]
            public ExportProvider ExportProvider { get; set; }
        }

        [Export, Shared, ExportMetadata("A", 1)]
        [MefV1.Export, MefV1.ExportMetadata("A", 1)]
        public class SomeOtherPart { }

        public interface SomeOtherPartMetadataView
        {
            int A { get; }
        }

        public interface MetadataViewWithBMember
        {
            int B { get; }
        }

        public class Fruit { }

        [Export("SomeContract")]
        [MefV1.Export("SomeContract")]
        public class Apple : Fruit { }

        #region Lazy activation tests

        /// <summary>
        /// Documents MEF v1 and v2 behavior that all parts are activated even before the enumeration is fully realized.
        /// See <see cref="GetExportedValuesActivatesPartsWithEnumeration"/> for V3 behavior.
        /// </summary>
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, typeof(Foo1), typeof(Foo2), NoCompatGoal = true)]
        public void GetExportedValuesActivatesAllReturnedParts(IContainer container)
        {
            Foo1.ActivationCounter = 0;
            Foo2.ActivationCounter = 0;
            var values = container.GetExportedValues<IFoo>();
            Assert.Equal(1, Foo1.ActivationCounter);
            Assert.Equal(1, Foo2.ActivationCounter);
        }

        /// <summary>
        /// Verifies that V3 emulates V1 correctly when using the V1 ExportProvider shim.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Foo1), typeof(Foo2))]
        public void GetExportedValuesActivatesAllReturnedPartsWithV1Shim(IContainer container)
        {
            var shim = container.GetExportedValue<ExportProvider>().AsExportProvider();

            Foo1.ActivationCounter = 0;
            Foo2.ActivationCounter = 0;
            var values = shim.GetExportedValues<IFoo>();
            Assert.Equal(1, Foo1.ActivationCounter);
            Assert.Equal(1, Foo2.ActivationCounter);
        }

        /// <summary>
        /// MEFv3 is more lazy at activating parts than MEFv1 and MEFv2.
        /// See <see cref="GetExportedValuesActivatesAllReturnedParts"/> for V1/V2 behavior.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(Foo1), typeof(Foo2))]
        public void GetExportedValuesActivatesPartsWithEnumeration(IContainer container)
        {
            var ep = container.GetExportedValue<ExportProvider>();

            Foo1.ActivationCounter = 0;
            Foo2.ActivationCounter = 0;
            var values = container.GetExportedValues<IFoo>();
            Assert.Equal(0, Foo1.ActivationCounter);
            Assert.Equal(0, Foo2.ActivationCounter);

            // We don't know what order these exports are in, but between the two of them,
            // exactly one should be activated when we enumerate one element.
            values.First();
            Assert.Equal(1, Foo1.ActivationCounter + Foo2.ActivationCounter);

            values.Skip(1).First();
            Assert.Equal(1, Foo1.ActivationCounter);
            Assert.Equal(1, Foo2.ActivationCounter);

            values.ToList(); // Enumerate everything again.
            Assert.Equal(1, Foo1.ActivationCounter);
            Assert.Equal(1, Foo2.ActivationCounter);
        }

        public interface IFoo { }

        [Export(typeof(IFoo)), Shared]
        [MefV1.Export(typeof(IFoo))]
        public class Foo1 : IFoo
        {
            public Foo1()
            {
                ActivationCounter++;
            }

            public static int ActivationCounter;
        }

        [Export(typeof(IFoo)), Shared]
        [MefV1.Export(typeof(IFoo))]
        public class Foo2 : IFoo
        {
            public Foo2()
            {
                ActivationCounter++;
            }

            public static int ActivationCounter;
        }

        #endregion

        #region NonShared parts activated once per query

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(NonSharedPart))]
        public void NonSharedPartActivatedOncePerNonLazyQuery(IContainer container)
        {
            var result = container.GetExportedValues<NonSharedPart>();

            var partInstanceFirst = result.Single();
            var partInstanceSecond = result.Single();
            Assert.Same(partInstanceFirst, partInstanceSecond);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(NonSharedPart))]
        public void NonSharedPartActivatedOncePerLazyQuery(IContainer container)
        {
            var result = container.GetExports<NonSharedPart>();

            var partInstanceFirst = result.Single().Value;
            var partInstanceSecond = result.Single().Value;
            Assert.Same(partInstanceFirst, partInstanceSecond);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPart { }

        #endregion
    }
}
