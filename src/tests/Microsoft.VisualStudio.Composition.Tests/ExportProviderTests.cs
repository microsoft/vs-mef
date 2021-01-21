// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class ExportProviderTests
    {
        [MefFact(CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1, typeof(PartThatImportsExportProvider), typeof(SomeOtherPart))]
        public void GetExportedValue_NonGeneric(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            var exportProvider = importer.ExportProvider;

            var importDefinition = new ImportDefinition(
                typeof(SomeOtherPart).FullName!,
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary<string, object?>.Empty,
                ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty);
            IEnumerable<Export> exports = exportProvider.GetExports(importDefinition);

            // Verify the re-enumeration does not recreate the objects.
            Assert.Same(exports.Single(), exports.Single());

            // Verify that getting the exported value returns the same value every time.
            Assert.Same(exports.Single().Value, exports.Single().Value);

            // Verify that the value isn't null.
            var otherPart2 = exports.Single().Value;
            Assert.NotNull(otherPart2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExport_WithMetadataDictionary(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, IDictionary<string, object?>>();
            Assert.Equal(1, export.Metadata["A"]);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExports_WithMetadataDictionary_NonGeneric(IContainer container)
        {
            var export = container.GetExports(typeof(SomeOtherPart), typeof(IDictionary<string, object?>), null).Single();
            Assert.Equal(1, ((IDictionary<string, object?>)export.Metadata)["A"]);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExport_WithMetadataView(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, ISomeOtherPartMetadataView>();
            Assert.Equal(1, export.Metadata.A);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExports_WithMetadataView_NonGeneric(IContainer container)
        {
            var export = container.GetExports(typeof(SomeOtherPart), typeof(ISomeOtherPartMetadataView), null).Single();
            Assert.Equal(1, ((ISomeOtherPartMetadataView)export.Metadata).A);
            Assert.NotNull(export.Value);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExport_WithInternalMetadataView(IContainer container)
        {
            var export = container.GetExport<SomeOtherPart, ISomeOtherPartInternalMetadataView>();
            Assert.Equal(1, export.Metadata.A);
            Assert.NotNull(export.Value);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExports_WithInternalMetadataView_NonGeneric(IContainer container)
        {
            var export = container.GetExports(typeof(SomeOtherPart), typeof(ISomeOtherPartInternalMetadataView), contractName: null).Single();
            Assert.Equal(1, ((ISomeOtherPartInternalMetadataView)export.Metadata).A);
            Assert.NotNull(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(SomeOtherPart))]
        public void GetExports_WithFilteringMetadataView(IContainer container)
        {
            var exports = container.GetExports<SomeOtherPart, IMetadataViewWithBMember>();
            Assert.Equal(0, exports.Count());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(Apple))]
        public void GetExportedValue_OfTypeByObjectAndContractName(IContainer container)
        {
            var apple = container.GetExportedValue<object>("SomeContract");
            Assert.IsType(typeof(Apple), apple);
        }

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(Apple))]
        public void GetExportedValues_OfTypeByObjectAndContractName_NonGeneric(IContainer container)
        {
            var apple = container.GetExportedValues(typeof(object), "SomeContract").Single();
            Assert.IsType(typeof(Apple), apple);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Apple))]
        public void GetExportedValues_OfTypeByBaseTypeAndContractName(IContainer container)
        {
            var apples = container.GetExportedValues<Fruit>("SomeContract");
            Assert.Empty(apples);
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V2Compat, typeof(Apple))]
        public void GetExportedValues_OfTypeByBaseTypeAndContractName_NonGeneric(IContainer container)
        {
            var apples = container.GetExportedValues(typeof(Fruit), "SomeContract");
            Assert.Empty(apples);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(SomeOtherPart))]
        public void GetExportedValueOfExportFactoryOfT(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<ExportFactory<SomeOtherPart>>());
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<MefV1.ExportFactory<SomeOtherPart>>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Apple))]
        public void GetExportedValue_OfFuncOfExportedType_Throws(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<Func<Apple>>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(Apple))]
        public void GetExport_OfFuncOfExportedType_Throws(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExport<Func<Apple>>());
        }

        [MefFact(CompositionEngines.V2, typeof(Apple), NoCompatGoal = true)]
        public void GetExport_OfFuncOfExportedType_ThrowsV2(IContainer container)
        {
            var foo = container.GetExport<Func<Apple>>();
            Assert.Throws<CompositionFailedException>(() => foo.Value());
        }

        #region GetExports of nonshared parts get disposed and released

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void GetExport_NonSharedPartExportNoLeakAfterReleaseLazy_Transitive(IContainer container)
        {
            (WeakReference part1, WeakReference part2) = GetExport_NonSharedPartExportNoLeakAfterReleaseLazy_Transitive_Helper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
            Assert.False(part2.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference, WeakReference) GetExport_NonSharedPartExportNoLeakAfterReleaseLazy_Transitive_Helper(IContainer container)
        {
            Lazy<NonSharedPartThatImportsAnotherNonSharedPart> export = container.GetExport<NonSharedPartThatImportsAnotherNonSharedPart>();
            Assert.NotNull(export.Value.NonSharedPart);
            WeakReference exportedValue = new WeakReference(export.Value);
            WeakReference transitiveExportedValue = new WeakReference(export.Value.NonSharedPart);

            // This should remove the exported part from the container.
            container.ReleaseExport(export);
            return (exportedValue, transitiveExportedValue);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(NonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void GetExport_NonDisposableNonSharedPartDoesNotLeakEvenWithoutReleaseLazy_Transitive(IContainer container)
        {
            WeakReference part1 = GetExport_NonDisposableNonSharedPartDoesNotLeakEvenWithoutReleaseLazy_Transitive_Helper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference GetExport_NonDisposableNonSharedPartDoesNotLeakEvenWithoutReleaseLazy_Transitive_Helper(IContainer container)
        {
            Lazy<NonSharedPart> export = container.GetExport<NonSharedPart>();
            Assert.NotNull(export.Value);
            return new WeakReference(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_NonSharedPartExportDisposedAfterReleaseLazy_Transitive(IContainer container)
        {
            Lazy<NonSharedPartThatImportsAnotherNonSharedPart> export = container.GetExport<NonSharedPartThatImportsAnotherNonSharedPart>();
            Assert.NotNull(export.Value.NonSharedPart);

            container.ReleaseExport(export);
            Assert.Equal(1, export.Value.DisposalCount);
            Assert.Equal(1, export.Value.NonSharedPart.DisposalCount);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_NonSharedPartExportDisposedAfterReleaseExport_Transitive(IContainer container)
        {
            string contractName = typeof(NonSharedPartThatImportsAnotherNonSharedPart).FullName!;
            NonSharedPartThatImportsAnotherNonSharedPart export;
            Action releaseExport;
            if (container is TestUtilities.V1ContainerWrapper v1container)
            {
                var v1Export = v1container.Container.GetExports(new MefV1.Primitives.ImportDefinition(ed => ed.ContractName == contractName, contractName, MefV1.Primitives.ImportCardinality.ExactlyOne, false, false)).Single();
                export = (NonSharedPartThatImportsAnotherNonSharedPart)v1Export.Value;
                releaseExport = () => v1container.Container.ReleaseExport(v1Export);
            }
            else if (container is TestUtilities.V3ContainerWrapper v3container)
            {
                var v3Export = v3container.ExportProvider.GetExports(new ImportDefinition(contractName, ImportCardinality.ExactlyOne, ImmutableDictionary<string, object?>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty)).Single();
                export = (NonSharedPartThatImportsAnotherNonSharedPart)v3Export.Value!;
                releaseExport = () => v3container.ExportProvider.ReleaseExport(v3Export);
            }
            else
            {
                throw new NotSupportedException();
            }

            Assert.NotNull(export.NonSharedPart);

            releaseExport();
            Assert.Equal(1, export.DisposalCount);
            Assert.Equal(1, export.NonSharedPart.DisposalCount);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void GetExports_NonSharedPartExportNoLeakAfterReleaseExport_Transitive(IContainer container)
        {
            (WeakReference part1, WeakReference part2) = GetExports_NonSharedPartExportNoLeakAfterReleaseExport_Transitive_Helper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
            Assert.False(part2.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference, WeakReference) GetExports_NonSharedPartExportNoLeakAfterReleaseExport_Transitive_Helper(IContainer container)
        {
            string contractName = typeof(NonSharedPartThatImportsAnotherNonSharedPart).FullName!;
            NonSharedPartThatImportsAnotherNonSharedPart value;
            Action releaseExport;
            if (container is TestUtilities.V1ContainerWrapper v1container)
            {
                var v1Export = v1container.Container.GetExports(new MefV1.Primitives.ImportDefinition(ed => ed.ContractName == contractName, contractName, MefV1.Primitives.ImportCardinality.ExactlyOne, false, false)).Single();
                value = (NonSharedPartThatImportsAnotherNonSharedPart)v1Export.Value;
                releaseExport = () => v1container.Container.ReleaseExport(v1Export);
            }
            else if (container is TestUtilities.V3ContainerWrapper v3container)
            {
                var v3Export = v3container.ExportProvider.GetExports(new ImportDefinition(contractName, ImportCardinality.ExactlyOne, ImmutableDictionary<string, object?>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty)).Single();
                value = (NonSharedPartThatImportsAnotherNonSharedPart)v3Export.Value!;
                releaseExport = () => v3container.ExportProvider.ReleaseExport(v3Export);
            }
            else
            {
                throw new NotSupportedException();
            }

            Assert.NotNull(value.NonSharedPart);
            WeakReference exportedValue = new WeakReference(value);
            WeakReference transitiveExportedValue = new WeakReference(value.NonSharedPart);

            // This should remove the exported part from the container.
            releaseExport();
            return (exportedValue, transitiveExportedValue);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExports_NonSharedPartExportDisposedAfterReleaseExport_Transitive(IContainer container)
        {
            string contractName = typeof(NonSharedPartThatImportsAnotherNonSharedPart).FullName!;
            NonSharedPartThatImportsAnotherNonSharedPart value;
            Action releaseExport;
            if (container is TestUtilities.V1ContainerWrapper v1container)
            {
                var v1Export = v1container.Container.GetExports(new MefV1.Primitives.ImportDefinition(ed => ed.ContractName == contractName, contractName, MefV1.Primitives.ImportCardinality.ExactlyOne, false, false)).Single();
                value = (NonSharedPartThatImportsAnotherNonSharedPart)v1Export.Value;
                releaseExport = () => v1container.Container.ReleaseExport(v1Export);
            }
            else if (container is TestUtilities.V3ContainerWrapper v3container)
            {
                var v3Export = v3container.ExportProvider.GetExports(new ImportDefinition(contractName, ImportCardinality.ExactlyOne, ImmutableDictionary<string, object?>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty)).Single();
                value = (NonSharedPartThatImportsAnotherNonSharedPart)v3Export.Value!;
                releaseExport = () => v3container.ExportProvider.ReleaseExport(v3Export);
            }
            else
            {
                throw new NotSupportedException();
            }

            Assert.NotNull(value.NonSharedPart);

            releaseExport();
            Assert.Equal(1, value.DisposalCount);
            Assert.Equal(1, value.NonSharedPart.DisposalCount);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(DisposableNonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void GetExports_UnactivatedExportCanBeCollected(IContainer container)
        {
            WeakReference part1 = GetExports_UnactivatedExportCanBeCollectedHelper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference GetExports_UnactivatedExportCanBeCollectedHelper(IContainer container)
        {
            string contractName = typeof(DisposableNonSharedPart).FullName!;
            if (container is TestUtilities.V1ContainerWrapper v1container)
            {
                var v1Export = v1container.Container.GetExports(new MefV1.Primitives.ImportDefinition(ed => ed.ContractName == contractName, contractName, MefV1.Primitives.ImportCardinality.ExactlyOne, false, false)).Single();
                return new WeakReference(v1Export);
            }
            else if (container is TestUtilities.V3ContainerWrapper v3container)
            {
                var v3Export = v3container.ExportProvider.GetExports(new ImportDefinition(contractName, ImportCardinality.ExactlyOne, ImmutableDictionary<string, object?>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty)).Single();
                return new WeakReference(v3Export);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        [MefFact(CompositionEngines.V2Compat, typeof(DisposableNonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void GetExports_UnactivatedLazyCanBeCollected(IContainer container)
        {
            WeakReference part1 = GetExports_UnactivatedLazyCanBeCollectedHelper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference GetExports_UnactivatedLazyCanBeCollectedHelper(IContainer container)
        {
            return new WeakReference(container.GetExport<DisposableNonSharedPart>());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("WeakReference", "true")]
        [Trait(Traits.SkipOnMono, "WeakReference")]
        public void GetExport_NonSharedPartExportNoLeakAfterReleaseLazyEnumerable_Transitive(IContainer container)
        {
            (WeakReference part1, WeakReference part2) = GetExport_NonSharedPartExportNoLeakAfterReleaseLazyEnumerable_TransitiveV1_Helper(container);
            GC.Collect();
            Assert.False(part1.IsAlive);
            Assert.False(part2.IsAlive);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference, WeakReference) GetExport_NonSharedPartExportNoLeakAfterReleaseLazyEnumerable_TransitiveV1_Helper(IContainer container)
        {
            Lazy<NonSharedPartThatImportsAnotherNonSharedPart> export = container.GetExport<NonSharedPartThatImportsAnotherNonSharedPart>();
            Assert.NotNull(export.Value.NonSharedPart);
            WeakReference exportedValue = new WeakReference(export.Value);
            WeakReference transitiveExportedValue = new WeakReference(export.Value.NonSharedPart);

            // This should remove the exported part from the container.
            container.ReleaseExports(new Lazy<NonSharedPartThatImportsAnotherNonSharedPart>[] { export });
            return (exportedValue, transitiveExportedValue);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsAnotherNonSharedPart), typeof(DisposableNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_NonSharedPartExportDisposedAfterReleaseLazyEnumerable_Transitive(IContainer container)
        {
            Lazy<NonSharedPartThatImportsAnotherNonSharedPart> export = container.GetExport<NonSharedPartThatImportsAnotherNonSharedPart>();
            Assert.NotNull(export.Value.NonSharedPart);

            container.ReleaseExports(new Lazy<NonSharedPartThatImportsAnotherNonSharedPart>[] { export });
            Assert.Equal(1, export.Value.DisposalCount);
            Assert.Equal(1, export.Value.NonSharedPart.DisposalCount);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartThatImportsNonSharedDisposableViaImportConstraint), typeof(DisposableMaybeSharedPartV1))]
        [Trait("Disposal", "")]
        public void GetExport_ConditionallyNonSharedPartExportDisposedAfterReleaseExport_Transitive(IContainer container)
        {
            Lazy<NonSharedPartThatImportsNonSharedDisposableViaImportConstraint> export = container.GetExport<NonSharedPartThatImportsNonSharedDisposableViaImportConstraint>();
            Assert.NotNull(export.Value.NonSharedPart);

            container.ReleaseExport(export);
            Assert.Equal(1, export.Value.NonSharedPart.DisposalCount);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(DisposableNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExports_ExportActivatedAfterContainerDisposal(IContainer container)
        {
            string contractName = typeof(DisposableNonSharedPart).FullName!;
            if (container is TestUtilities.V1ContainerWrapper v1container)
            {
                var v1Export = v1container.Container.GetExports(new MefV1.Primitives.ImportDefinition(ed => ed.ContractName == contractName, contractName, MefV1.Primitives.ImportCardinality.ExactlyOne, false, false)).Single();
                container.Dispose();
                Assert.Throws<ObjectDisposedException>(() => v1Export.Value);
            }
            else if (container is TestUtilities.V3ContainerWrapper v3container)
            {
                var v3Export = v3container.ExportProvider.GetExports(new ImportDefinition(contractName, ImportCardinality.ExactlyOne, ImmutableDictionary<string, object?>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty)).Single();
                container.Dispose();
                Assert.Throws<ObjectDisposedException>(() => v3Export.Value);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        [MefFact(CompositionEngines.V2, typeof(NonSharedPart), NoCompatGoal = true)]
        [Trait("Disposal", "")]
        public void GetExport_LazyActivatedAfterContainerDisposalV2(IContainer container)
        {
            Lazy<NonSharedPart> export = container.GetExport<NonSharedPart>();
            container.Dispose();
            NonSharedPart value = export.Value;
        }

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_LazyActivatedAfterContainerDisposal(IContainer container)
        {
            Lazy<NonSharedPart> export = container.GetExport<NonSharedPart>();
            container.Dispose();
            Assert.Throws<ObjectDisposedException>(() => export.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(DisposablePartWithPropertyExportingNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_LazyActivatedAfterContainerDisposal_MemberExport(IContainer container)
        {
            Lazy<ConstructedValue> export = container.GetExport<ConstructedValue>();
            container.Dispose();
            Assert.Throws<ObjectDisposedException>(() => export.Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(DisposableNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_LazyActivatedAfterContainerDisposal_DisposablePart(IContainer container)
        {
            Lazy<DisposableNonSharedPart> export = container.GetExport<DisposableNonSharedPart>();
            container.Dispose();
            Assert.Throws<ObjectDisposedException>(() => export.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(DisposablePartWithPropertyExportingNonSharedPart))]
        [Trait("Disposal", "")]
        public void GetExport_NonSharedDisposablePartWithExportingMember_DisposedWhenExportReleased(IContainer container)
        {
            Lazy<ConstructedValue> export = container.GetExport<ConstructedValue>();
            Assert.NotNull(export.Value);
            container.ReleaseExport(export);
            Assert.Equal(1, ((DisposablePartWithPropertyExportingNonSharedPart)export.Value.Owner).DisposalCount);
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        [Export]
        public class NonSharedPartThatImportsAnotherNonSharedPart : IDisposable
        {
            [Import, MefV1.Import]
            public DisposableNonSharedPart NonSharedPart { get; set; } = null!;

            internal int DisposalCount { get; private set; }

            public void Dispose() => this.DisposalCount++;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class DisposableNonSharedPart : IDisposable
        {
            public int DisposalCount { get; private set; }

            public void Dispose() => this.DisposalCount++;
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPartThatImportsNonSharedDisposableViaImportConstraint
        {
            [MefV1.Import(RequiredCreationPolicy = MefV1.CreationPolicy.NonShared)]
            public DisposableMaybeSharedPartV1 NonSharedPart { get; private set; } = null!;
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.Any)]
        public class DisposableMaybeSharedPartV1 : IDisposable
        {
            public int DisposalCount { get; private set; }

            public void Dispose() => this.DisposalCount++;
        }

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class DisposablePartWithPropertyExportingNonSharedPart : IDisposable
        {
            [MefV1.Export, Export]
            [MefV1.ExportMetadata("N", "V")]
            [ExportMetadata("N", "V")]
            public ConstructedValue ExportingProperty => new ConstructedValue(this);

            internal int DisposalCount { get; private set; }

            public void Dispose() => this.DisposalCount++;
        }

        public class ConstructedValue
        {
            internal ConstructedValue(object owner)
            {
                this.Owner = owner;
            }

            public object Owner { get; }
        }

        #endregion

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsExportProvider
        {
            [Import, MefV1.Import]
            public ExportProvider ExportProvider { get; set; } = null!;
        }

        [Export, Shared, ExportMetadata("A", 1)]
        [MefV1.Export, MefV1.ExportMetadata("A", 1)]
        public class SomeOtherPart { }

        public interface ISomeOtherPartMetadataView
        {
            int A { get; }
        }

        internal interface ISomeOtherPartInternalMetadataView
        {
            int A { get; }
        }

        public interface IMetadataViewWithBMember
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

        #region GUID contract name

        [MefFact(CompositionEngines.V2Compat, typeof(SomeExportedPartWithGuidContractName))]
        public void GuidContractName(IContainer container)
        {
            var part = container.GetExportedValue<SomeExportedPartWithGuidContractName>("{C18E5D73-E6D1-43AA-AC5E-58D82E44DA9C}");
        }

        [Export("{C18E5D73-E6D1-43AA-AC5E-58D82E44DA9C}")]
        public class SomeExportedPartWithGuidContractName { }

        #endregion

        #region Static exports tests

        [MefFact(CompositionEngines.V1Compat, typeof(StaticPart))]
        public void GetExports_StaticExports(IContainer container)
        {
            List<string> exports = container.GetExportedValues<string>().ToList();
            Assert.Equal(1, exports.Count);
            Assert.Equal("PASS", exports[0]);
        }

        public static class StaticPart
        {
            [MefV1.Export]
            public static string Foo
            {
                get { return "PASS"; }
            }
        }

        #endregion
    }
}
