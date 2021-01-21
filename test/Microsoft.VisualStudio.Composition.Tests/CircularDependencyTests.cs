// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public class CircularDependencyTests
    {
        #region Tight loop of all shared exports

        [MefFact(CompositionEngines.V2Compat, typeof(SharedExport1), typeof(SharedExport2))]
        public void CircularDependenciesSharedExports(IContainer container)
        {
            var export1 = container.GetExportedValue<SharedExport1>();
            var export2 = container.GetExportedValue<SharedExport2>();
            Assert.Same(export1.Export2, export2);
            Assert.Same(export2.Export1, export1);
        }

        [Export, Shared]
        public class SharedExport1
        {
            [Import]
            public SharedExport2 Export2 { get; set; } = null!;
        }

        [Export, Shared]
        public class SharedExport2
        {
            [Import]
            public SharedExport1 Export1 { get; set; } = null!;
        }

        #endregion

        #region Tight loop of all shared (internal) exports

        [MefFact(CompositionEngines.V1Compat, typeof(InternalSharedExport1), typeof(InternalSharedExport2))]
        public void CircularDependenciesInternalSharedExports(IContainer container)
        {
            var export1 = container.GetExportedValue<InternalSharedExport1>();
            var export2 = container.GetExportedValue<InternalSharedExport2>();
            Assert.Same(export1.Export2, export2);
            Assert.Same(export2.Export1, export1);
        }

        [MefV1.Export]
        internal class InternalSharedExport1
        {
            [MefV1.Import]
            public InternalSharedExport2 Export2 { get; set; } = null!;
        }

        [MefV1.Export]
        internal class InternalSharedExport2
        {
            [MefV1.Import]
            public InternalSharedExport1 Export1 { get; set; } = null!;
        }

        #endregion

        #region Tight loop of all shared lazy exports

        [MefFact(CompositionEngines.V2Compat, typeof(LazySharedExport1), typeof(LazySharedExport2))]
        public void CircularDependenciesLazySharedExports(IContainer container)
        {
            var export1 = container.GetExportedValue<LazySharedExport1>();
            var export2 = container.GetExportedValue<LazySharedExport2>();
            Assert.Same(export1.Export2.Value, export2);
            Assert.Same(export2.Export1.Value, export1);
        }

        [Export, Shared]
        public class LazySharedExport1
        {
            [Import]
            public Lazy<LazySharedExport2> Export2 { get; set; } = null!;
        }

        [Export, Shared]
        public class LazySharedExport2
        {
            [Import]
            public Lazy<LazySharedExport1> Export1 { get; set; } = null!;
        }

        #endregion

        #region Loop with just one shared export

        [MefFact(CompositionEngines.V2Compat, typeof(SharedExportInLoopOfNonShared), typeof(NonSharedExportInLoopWithShared), typeof(AnotherNonSharedExportInLoopWithShared))]
        public void CircularDependenciesOneSharedExport(IContainer container)
        {
            var shared = container.GetExportedValue<SharedExportInLoopOfNonShared>();
            var nonShared = container.GetExportedValue<NonSharedExportInLoopWithShared>();
            var anotherNonShared = container.GetExportedValue<AnotherNonSharedExportInLoopWithShared>();
            Assert.NotSame(nonShared, shared.Other);
            Assert.NotSame(anotherNonShared, nonShared.Another);
            Assert.NotSame(anotherNonShared, shared.Other.Another);
            Assert.Same(shared, anotherNonShared.Shared);
        }

        [Export, Shared]
        public class SharedExportInLoopOfNonShared
        {
            [Import]
            public NonSharedExportInLoopWithShared Other { get; set; } = null!;
        }

        [Export]
        public class NonSharedExportInLoopWithShared
        {
            [Import]
            public AnotherNonSharedExportInLoopWithShared Another { get; set; } = null!;
        }

        [Export]
        public class AnotherNonSharedExportInLoopWithShared
        {
            [Import]
            public SharedExportInLoopOfNonShared Shared { get; set; } = null!;
        }

        #endregion

        #region Tight loop of all Any parts, imported as non-shared

        [MefFact(CompositionEngines.V1Compat, typeof(AnyPolicyPart1), typeof(AnyPolicyPart2), InvalidConfiguration = true)]
        public void CircularDependenciesAnyExportsImportedAsAsNonShared(IContainer container)
        {
            container.GetExportedValue<AnyPolicyPart1>();
        }

        [MefV1.Export]
        public class AnyPolicyPart1
        {
            [MefV1.Import(RequiredCreationPolicy = MefV1.CreationPolicy.NonShared)]
            public AnyPolicyPart2 Export2 { get; set; } = null!;
        }

        [MefV1.Export]
        public class AnyPolicyPart2
        {
            [MefV1.Import(RequiredCreationPolicy = MefV1.CreationPolicy.NonShared)]
            public AnyPolicyPart1 Export1 { get; set; } = null!;
        }

        #endregion

        #region Large loop of all non-shared exports

        [MefFact(CompositionEngines.V2Compat, typeof(NonSharedPart1), typeof(NonSharedPart2), typeof(NonSharedPart3), InvalidConfiguration = true)]
        public void LoopOfNonSharedExports(IContainer container)
        {
            // There is no way to resolve this loop. It would instantiate parts forever.
            container.GetExportedValue<NonSharedPart1>();
        }

        [Export]
        public class NonSharedPart1
        {
            [Import]
            public NonSharedPart2 Part2 { get; set; } = null!;
        }

        [Export]
        public class NonSharedPart2
        {
            [Import]
            public NonSharedPart3 Part3 { get; set; } = null!;
        }

        [Export]
        public class NonSharedPart3
        {
            [Import]
            public NonSharedPart1 Part1 { get; set; } = null!;
        }

        #endregion

        #region Imports self

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SelfImportingNonSharedPart), InvalidConfiguration = true)]
        public void SelfImportingNonSharedPartIsInvalid(IContainer container)
        {
            container.GetExportedValue<SelfImportingNonSharedPart>();
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SelfImportingNonSharedPartViaExportingProperty), InvalidConfiguration = true)]
        public void SelfImportingNonSharedPartViaExportingPropertyThrows(IContainer container)
        {
            container.GetExportedValue<SelfImportingNonSharedPartViaExportingProperty>();
        }

        [MefFact(CompositionEngines.V2Compat | CompositionEngines.V1Compat, typeof(SelfImportingSharedPart))]
        public void SelfImportingSharedPartSucceeds(IContainer container)
        {
            var value = container.GetExportedValue<SelfImportingSharedPart>();
            Assert.Same(value, value.Self);
            Assert.Same(value, value.LazySelf.Value);
            Assert.Same(value, value.LazySelfMetadata.Value);
            Assert.Equal(1, value.SelfMany.Length);
            Assert.Same(value, value.SelfMany[0]);
            Assert.Equal(1, value.LazySelfMany.Length);
            Assert.Same(value, value.LazySelfMany[0].Value);
            Assert.Equal(1, value.LazySelfManyMetadata.Length);
            Assert.Same(value, value.LazySelfManyMetadata[0].Value);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class SelfImportingNonSharedPart
        {
            [Import]
            [MefV1.Import]
            public SelfImportingNonSharedPart Self { get; set; } = null!;
        }

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class SelfImportingNonSharedPartViaExportingProperty
        {
            [Export]
            [MefV1.Export]
            public SelfImportingNonSharedPartViaExportingProperty SelfExport
            {
                get { return this; }
            }

            [Import]
            [MefV1.Import]
            public SelfImportingNonSharedPartViaExportingProperty SelfImport { get; set; } = null!;
        }

        [MefV1.Export]
        [Export, Shared]
        public class SelfImportingSharedPart
        {
            [Import]
            [MefV1.Import]
            public SelfImportingSharedPart Self { get; set; } = null!;

            [Import]
            [MefV1.Import]
            public Lazy<SelfImportingSharedPart> LazySelf { get; set; } = null!;

            [Import]
            [MefV1.Import]
            public Lazy<SelfImportingSharedPart, IDictionary<string, object?>> LazySelfMetadata { get; set; } = null!;

            [ImportMany]
            [MefV1.ImportMany]
            public SelfImportingSharedPart[] SelfMany { get; set; } = null!;

            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<SelfImportingSharedPart>[] LazySelfMany { get; set; } = null!;

            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<SelfImportingSharedPart, IDictionary<string, object?>>[] LazySelfManyMetadata { get; set; } = null!;
        }

        #endregion

        #region No loop, but with multiple paths to a common import

        [Fact]
        public async Task ValidMultiplePaths()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(await TestUtilities.V2Discovery.CreatePartsAsync(
                typeof(ValidMultiplePathRoot),
                typeof(ValidMultiplePathTrail1),
                typeof(ValidMultiplePathTrail2),
                typeof(ValidMultiplePathCommonImport)));
            CompositionConfiguration.Create(catalog);
        }

        [Export]
        public class ValidMultiplePathRoot
        {
            [Import]
            public ValidMultiplePathTrail1 Trail1 { get; set; } = null!;

            [Import]
            public ValidMultiplePathTrail2 Trail2 { get; set; } = null!;
        }

        [Export]
        public class ValidMultiplePathTrail1
        {
            [Import]
            public ValidMultiplePathCommonImport ImportingProperty { get; set; } = null!;
        }

        [Export]
        public class ValidMultiplePathTrail2
        {
            [Import]
            public ValidMultiplePathCommonImport ImportingProperty { get; set; } = null!;
        }

        [Export]
        public class ValidMultiplePathCommonImport { }

        #endregion

        #region Loop involving one importing constructor and a non-lazy import

        [MefFact(CompositionEngines.V2, typeof(PartWithImportingProperty), typeof(PartWithImportingConstructor), NoCompatGoal = true)]
        public void LoopWithImportingConstructorAndImportingPropertyV2(IContainer container)
        {
            var partWithImportingProperty = container.GetExportedValue<PartWithImportingProperty>();
            Assert.NotNull(partWithImportingProperty.PartWithImportingConstructor);
            Assert.Same(partWithImportingProperty, partWithImportingProperty.PartWithImportingConstructor.PartWithImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithImportingProperty), typeof(PartWithImportingConstructor), InvalidConfiguration = true)]
        public void LoopWithImportingConstructorAndImportingPropertyV1(IContainer container)
        {
            var partWithImportingProperty = container.GetExportedValue<PartWithImportingProperty>();
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingProperty
        {
            [Import, MefV1.Import]
            public PartWithImportingConstructor PartWithImportingConstructor { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructor
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructor(PartWithImportingProperty other)
            {
                this.PartWithImportingProperty = other;

                // It's impossible to resolve this circular dependency without passing in an uninitialized "other".
                // It can't get an instance of "this" to stick there.
                Assert.Null(other.PartWithImportingConstructor);
            }

            public PartWithImportingProperty PartWithImportingProperty { get; set; }
        }

        #endregion

        #region Loop involving one importing constructor and a lazy import

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithLazyImportingProperty), typeof(PartWithImportingConstructorOfPartWithLazyImportingProperty))]
        public void LoopWithImportingConstructorAndLazyImportingProperty(IContainer container)
        {
            var partWithImportingProperty = container.GetExportedValue<PartWithLazyImportingProperty>();
            Assert.NotNull(partWithImportingProperty.PartWithImportingConstructor);

            // Verify the Lazy has the proper value, even after having previously thrown
            // in the part's constructor.
            Assert.Same(partWithImportingProperty, partWithImportingProperty.PartWithImportingConstructor.Value.PartWithLazyImportingProperty);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithLazyImportingProperty
        {
            [Import, MefV1.Import]
            public Lazy<PartWithImportingConstructorOfPartWithLazyImportingProperty> PartWithImportingConstructor { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartWithLazyImportingProperty
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartWithLazyImportingProperty(PartWithLazyImportingProperty other)
            {
                this.PartWithLazyImportingProperty = other;
                Assert.NotNull(other.PartWithImportingConstructor);

                // This not only verifies that we throw appropriately, but it proves later
                // that it doesn't break the lazy's ability to produce the correct value later
                // when the test method evaluates it again.
                // This is possible by constructing Lazy<T> with System.Threading.LazyThreadSafetyMode.PublicationOnly
                Assert.Throws<InvalidOperationException>(() => other.PartWithImportingConstructor.Value);
            }

            public PartWithLazyImportingProperty PartWithLazyImportingProperty { get; set; }
        }

        #endregion

        #region LoopWithImportingConstructorAndLazyImportPropertyOfPartiallyInitializedPart

        /// <summary>
        /// Verifies that initializing a part with an importing constructor works even
        /// when there is a loop that involves a lazy when the lazily initialized part is half-initialized.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithImportingConstructorOfPartWithLazyImportOfPartiallyInitializedPart), typeof(PartWithLazyImportOfPartiallyInitializedPart), typeof(PartiallyInitializedPart))]
        public void LoopWithImportingConstructorAndLazyImportPropertyOfPartiallyInitializedPart(IContainer container)
        {
            var a = container.GetExportedValue<PartiallyInitializedPart>();
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingConstructorOfPartWithLazyImportOfPartiallyInitializedPart
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithImportingConstructorOfPartWithLazyImportOfPartiallyInitializedPart(PartWithLazyImportOfPartiallyInitializedPart b)
            {
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithLazyImportOfPartiallyInitializedPart
        {
            [Import, MefV1.Import]
            public Lazy<PartiallyInitializedPart> C { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartiallyInitializedPart
        {
            [Import, MefV1.Import]
            public PartWithImportingConstructorOfPartWithLazyImportOfPartiallyInitializedPart A { get; set; } = null!;
        }

        #endregion

        #region Loop involving one importing constructor with a lazy import, and a part with a non-lazy import

        // V1 cannot handle the Lazy being evaluated in the constructor IFF we query the container for the importing property part.
        // V2 in a really super-freaky way nests a second shared part ctor invocation, which we do NOT want to emulate.
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2, typeof(PartWithImportingPropertyOfLazyImportingConstructor), typeof(PartWithLazyImportingConstructorOfPartWithImportingProperty))]
        public void LoopWithLazyImportingConstructorAndImportingPropertyQueryForPartWithImportingProperty(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() =>
                container.GetExportedValue<PartWithLazyImportingConstructorOfPartWithImportingProperty>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithImportingPropertyOfLazyImportingConstructor), typeof(PartWithLazyImportingConstructorOfPartWithImportingProperty))]
        public void LoopWithLazyImportingConstructorAndImportingPropertyQueryForPartWithImportingConstructor(IContainer container)
        {
            var partWithImportingProperty = container.GetExportedValue<PartWithImportingPropertyOfLazyImportingConstructor>();
            Assert.NotNull(partWithImportingProperty.PartWithImportingConstructor);
            Assert.Same(partWithImportingProperty, partWithImportingProperty.PartWithImportingConstructor.PartWithImportingProperty.Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithImportingPropertyOfLazyImportingConstructor
        {
            [Import, MefV1.Import]
            public PartWithLazyImportingConstructorOfPartWithImportingProperty PartWithImportingConstructor { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithLazyImportingConstructorOfPartWithImportingProperty
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartWithLazyImportingConstructorOfPartWithImportingProperty(Lazy<PartWithImportingPropertyOfLazyImportingConstructor> other)
            {
                this.PartWithImportingProperty = other;

                // This cannot possibly be non-null because until this constructor returns,
                // there is no value to assign to it.
                Assert.Null(other.Value.PartWithImportingConstructor);
            }

            public Lazy<PartWithImportingPropertyOfLazyImportingConstructor> PartWithImportingProperty { get; set; }
        }

        #endregion

        #region Loop involving an non-shared parts and ExportFactory<T>

        [MefFact(CompositionEngines.V1Compat, typeof(NonSharedPartWithExportFactory), typeof(PartConstructedByExportFactory))]
        public void LoopWithNonSharedPartsAndExportFactory(IContainer container)
        {
            var factory = container.GetExportedValue<NonSharedPartWithExportFactory>();
            var constructedPart = factory.Factory.CreateExport().Value;
            Assert.NotSame(factory, constructedPart.NonSharedPartWithExportFactory);
        }

        [MefV1.Export]
        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPartWithExportFactory
        {
            [MefV1.Import]
            public MefV1.ExportFactory<PartConstructedByExportFactory> Factory { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartConstructedByExportFactory
        {
            [MefV1.Import]
            public NonSharedPartWithExportFactory NonSharedPartWithExportFactory { get; set; } = null!;
        }

        #endregion

        #region Loop involving an ImportingConstructor and ExportFactory<T>

        [MefFact(CompositionEngines.V1Compat, typeof(SharedPartWithExportFactory), typeof(PartConstructedBySharedExportFactory), typeof(PartWithImportingConstructorInLoopWithExportFactory))]
        public void LoopWithImportingConstructorAndExportFactory(IContainer container)
        {
            var root = container.GetExportedValue<PartWithImportingConstructorInLoopWithExportFactory>();
            var factory = root.ExportFactoryPart;
            var constructedPart = factory.Factory.CreateExport().Value;
            Assert.Same(factory, constructedPart.NonSharedPartWithExportFactory.ExportFactoryPart);
        }

        [MefV1.Export]
        public class SharedPartWithExportFactory
        {
            [MefV1.Import]
            public MefV1.ExportFactory<PartConstructedBySharedExportFactory> Factory { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartConstructedBySharedExportFactory
        {
            [MefV1.Import]
            public PartWithImportingConstructorInLoopWithExportFactory NonSharedPartWithExportFactory { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartWithImportingConstructorInLoopWithExportFactory
        {
            [MefV1.ImportingConstructor]
            public PartWithImportingConstructorInLoopWithExportFactory(SharedPartWithExportFactory factory)
            {
                this.ExportFactoryPart = factory;
            }

            public SharedPartWithExportFactory ExportFactoryPart { get; private set; }
        }

        #endregion

        #region Unresolvable, non-analyzable circular dependency test

        [MefFact(CompositionEngines.V1, typeof(RootPartThatImperativelyQueriesForPartWithImportingConstructor), typeof(PartThatImportsRootPartViaImportingConstructor))]
        public void NonAnalyzableCircularDependencyFromImperativeQueryWithImportingConstructor(IContainer container)
        {
            RootPartThatImperativelyQueriesForPartWithImportingConstructor.ContainerForRunningTest = container;
            Assert.Throws<Microsoft.VisualStudio.Composition.CompositionFailedException>(() => container.GetExportedValue<RootPartThatImperativelyQueriesForPartWithImportingConstructor>());
        }

        [MefV1.Export]
        public class RootPartThatImperativelyQueriesForPartWithImportingConstructor
        {
            internal static IContainer? ContainerForRunningTest;

            public RootPartThatImperativelyQueriesForPartWithImportingConstructor()
            {
                // This matches what Microsoft.VisualStudio.Web.Application GetNugetProjectTypeContext is doing when it uses
                // IComponentModel.GetService<VsPackageInstallerServices>() on the callstack above the SolutionManager constructor.
                var nonAnalyzableDependency = ContainerForRunningTest!.GetExportedValue<PartThatImportsRootPartViaImportingConstructor>();
            }
        }

        [MefV1.Export]
        public class PartThatImportsRootPartViaImportingConstructor
        {
            [MefV1.ImportingConstructor]
            public PartThatImportsRootPartViaImportingConstructor(RootPartThatImperativelyQueriesForPartWithImportingConstructor rootPart)
            {
            }
        }

        #endregion

        #region Semi-resolvable, non-analyzable circular dependency test

        [MefFact(CompositionEngines.V1, typeof(RootPartThatImperativelyQueriesForPartWithImportingProperty), typeof(PartThatImportsRootPartViaImportingProperty))]
        public void NonAnalyzableCircularDependencyFromImperativeQueryWithImportingProperty(IContainer container)
        {
            RootPartThatImperativelyQueriesForPartWithImportingProperty.CtorCounter = 0;
            RootPartThatImperativelyQueriesForPartWithImportingProperty.ContainerForRunningTest = container;
            var export = container.GetExportedValue<RootPartThatImperativelyQueriesForPartWithImportingProperty>();

            // Make sure that despite MEFv1 tricks, that we get to see only one instance of the shared part.
            Assert.Same(export, export.ImperativelyAcquiredExport.RootPart);

            // Interestingly enough, MEFv1 resolves this dependency by creating a *second* instance of the shared part.
            // We assert here not because we care so much about their technique (in fact I dislike it),
            // but to document their behavior.
            Assert.Equal(2, RootPartThatImperativelyQueriesForPartWithImportingProperty.CtorCounter);
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.Shared)]
        public class RootPartThatImperativelyQueriesForPartWithImportingProperty
        {
            internal static int CtorCounter;
            internal static IContainer? ContainerForRunningTest;

            public RootPartThatImperativelyQueriesForPartWithImportingProperty()
            {
                CtorCounter++;

                // This matches what Microsoft.VisualStudio.Web.Application GetNugetProjectTypeContext is doing when it uses
                // IComponentModel.GetService<VsPackageInstallerServices>() on the callstack above the SolutionManager constructor.
                var nonAnalyzableDependency = ContainerForRunningTest!.GetExportedValue<PartThatImportsRootPartViaImportingProperty>();
                this.ImperativelyAcquiredExport = nonAnalyzableDependency;
            }

            public PartThatImportsRootPartViaImportingProperty ImperativelyAcquiredExport { get; set; }
        }

        [MefV1.Export]
        public class PartThatImportsRootPartViaImportingProperty
        {
            [MefV1.Import]
            internal RootPartThatImperativelyQueriesForPartWithImportingProperty RootPart { get; set; } = null!;
        }

        #endregion
    }
}
