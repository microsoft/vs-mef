// Copyright (c) Microsoft. All rights reserved.

#if DESKTOP

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV2 = System.Composition;

    public class CompositionServiceTests
    {
        [MefFact(CompositionEngines.V1Compat, new Type[0])]
        public void GetExportsOfICompositionService(IContainer container)
        {
            var service = container.GetExportedValue<ICompositionService>();
            Assert.NotNull(service);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(CompositionServiceImportingPart))]
        public void ImportCompositionService(IContainer container)
        {
            var part = container.GetExportedValue<CompositionServiceImportingPart>();
            Assert.NotNull(part.CompositionService);
        }

        /// <summary>
        /// Verifies that SatisfyImportsOnce functions correctly with an object
        /// that was included during catalog discovery with Import attributes on its members.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat, typeof(CompositionServiceImportingPart), typeof(ImportOnlyPart))]
        public void SatisfyImportsOnceWithDiscoveredImportOnlyPart(IContainer container)
        {
            var exportedPart = container.GetExportedValue<CompositionServiceImportingPart>();
            ICompositionService compositionService = exportedPart.CompositionService;

            var value = new ImportOnlyPart();
            compositionService.SatisfyImportsOnce(value);
            Assert.NotNull(value.SomePropertyThatImports);
        }

        /// <summary>
        /// Verifies that SatisfyImportsOnce functions correctly with an arbitrary object
        /// with Import attributes on its members.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat, typeof(CompositionServiceImportingPart))] // intentionally leaves out ImportOnlyPart
        public void SatisfyImportsOnceWithUnknownImportOnlyPart(IContainer container)
        {
            var exportedPart = container.GetExportedValue<CompositionServiceImportingPart>();
            ICompositionService compositionService = exportedPart.CompositionService;

            var value = new ImportOnlyPart();
            compositionService.SatisfyImportsOnce(value);
            Assert.NotNull(value.SomePropertyThatImports);
        }

        [Fact]
        public void AddCompositionServiceToCatalogTwice()
        {
            var catalog = TestUtilities.EmptyCatalog;
            var catalog1 = catalog.WithCompositionService();
            var catalog2 = catalog1.WithCompositionService();

            Assert.NotSame(catalog, catalog1);
            Assert.Same(catalog1, catalog2);
        }

        [Export]
        public class CompositionServiceImportingPart
        {
            [Import]
            public ICompositionService CompositionService { get; set; }
        }

        public class ImportOnlyPart
        {
            [Import]
            public CompositionServiceImportingPart SomePropertyThatImports { get; set; }
        }

        #region Sharing boundary tests

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(RootPart), typeof(AnotherRootPart), typeof(SubScopedPart), typeof(AnotherSubScopedPart), Skip = "Not important, and not obtainable for now.")]
        public void CompositionServiceSharedWithinRootScope(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var anotherRootPart = container.GetExportedValue<AnotherRootPart>();
            Assert.Same(root.CompositionService, anotherRootPart.CompositionService);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(RootPart), typeof(AnotherRootPart), typeof(SubScopedPart), typeof(AnotherSubScopedPart), Skip = "Not important, and not obtainable for now.")]
        public void CompositionServiceSharedWithinChildScope(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var scopedPart = root.ScopeFactory.CreateExport().Value;
            Assert.Same(scopedPart.CompositionService, scopedPart.AnotherSubScopedPart.CompositionService);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(RootPart), typeof(AnotherRootPart), typeof(SubScopedPart), typeof(AnotherSubScopedPart))]
        public void CompositionServiceUniqueAcrossScopes(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var scope1Part = root.ScopeFactory.CreateExport().Value;
            var scope2Part = root.ScopeFactory.CreateExport().Value;
            Assert.NotSame(scope1Part.CompositionService, scope2Part.CompositionService);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(RootPart), typeof(AnotherRootPart), typeof(SubScopedPart), typeof(AnotherSubScopedPart))]
        public void CompositionServiceFromRootSatisfiesRootImports(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();

            var myOwnRoot = new RootPart();
            root.CompositionService.SatisfyImportsOnce(myOwnRoot);
            Assert.NotNull(myOwnRoot.CompositionService);
            Assert.Same(root.AnotherRootPart, myOwnRoot.AnotherRootPart);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(RootPart), typeof(AnotherRootPart), typeof(SubScopedPart), typeof(AnotherSubScopedPart))]
        public void CompositionServiceFromRootDoesNotSatisfySubScopeImports(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var objectWithSubScopedImports = new SubScopedPart();
            Assert.Throws<CompositionFailedException>(() => root.CompositionService.SatisfyImportsOnce(objectWithSubScopedImports));
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(RootPart), typeof(AnotherRootPart), typeof(SubScopedPart), typeof(AnotherSubScopedPart))]
        public void CompositionServiceFromSubScopeSatisfiesSubScopeImports(IContainer container)
        {
            var root = container.GetExportedValue<RootPart>();
            var scope1Part = root.ScopeFactory.CreateExport().Value;
            var scope1Importer = new SubScopedPart();
            scope1Part.CompositionService.SatisfyImportsOnce(scope1Importer);
            Assert.Same(scope1Part.AnotherSubScopedPart, scope1Importer.AnotherSubScopedPart);
            Assert.Same(root, scope1Importer.Root);

            // Also make sure that another scope gets distinct exports
            var scope2Part = root.ScopeFactory.CreateExport().Value;
            var scope2Importer = new SubScopedPart();
            scope2Part.CompositionService.SatisfyImportsOnce(scope2Importer);
            Assert.Same(scope2Part.AnotherSubScopedPart, scope2Importer.AnotherSubScopedPart);
            Assert.NotSame(scope1Part.AnotherSubScopedPart, scope2Part.AnotherSubScopedPart);
            Assert.Same(root, scope2Importer.Root);
        }

        [MefV2.Export, MefV2.Shared]
        public class RootPart
        {
            [MefV2.Import, MefV2.SharingBoundary("a")]
            public MefV2.ExportFactory<SubScopedPart> ScopeFactory { get; set; }

            [Import, MefV2.Import]
            public ICompositionService CompositionService { get; set; }

            [Import, MefV2.Import]
            public AnotherRootPart AnotherRootPart { get; set; }
        }

        [MefV2.Export, MefV2.Shared]
        public class AnotherRootPart
        {
            [Import, MefV2.Import]
            public ICompositionService CompositionService { get; set; }
        }

        [MefV2.Export, MefV2.Shared("a")]
        public class SubScopedPart
        {
            [Import, MefV2.Import]
            public RootPart Root { get; set; }

            [Import, MefV2.Import]
            public AnotherSubScopedPart AnotherSubScopedPart { get; set; }

            [Import, MefV2.Import]
            public ICompositionService CompositionService { get; set; }
        }

        [MefV2.Export, MefV2.Shared("a")]
        public class AnotherSubScopedPart
        {
            [Import, MefV2.Import]
            public ICompositionService CompositionService { get; set; }
        }

        #endregion

        #region Crossing Scope LifeTime Test

        [MefFact(CompositionEngines.V2Compat, typeof(RootScopePart), typeof(RootScopeSecondPart), typeof(SubScopedBPart))]
        public void PartsInRootScopeStaysAlive(IContainer container)
        {
            var root = container.GetExportedValue<RootScopePart>();
            RootScopeSecondPart secondItem;
            using (var firstItem = root.ScopeFactory.CreateExport())
            {
                secondItem = firstItem.Value.Root;
            }

            using (var newItem = root.ScopeFactory.CreateExport())
            {
                Assert.Same(secondItem, newItem.Value.Root);
                Assert.False(newItem.Value.Root.IsDisposed);
            }

            container.Dispose();
            Assert.True(secondItem.IsDisposed);
        }

        [MefV2.Export, MefV2.Shared]
        public class RootScopePart
        {
            [MefV2.Import, MefV2.SharingBoundary("B")]
            public MefV2.ExportFactory<SubScopedBPart> ScopeFactory { get; set; }
        }

        [MefV2.Export, MefV2.Shared]
        public class RootScopeSecondPart : IDisposable
        {
            [MefV2.Import]
            public RootScopePart Root { get; set; }

            public bool IsDisposed { get; private set; }

            void IDisposable.Dispose()
            {
                this.IsDisposed = true;
            }
        }

        [MefV2.Export, MefV2.Shared("B")]
        public class SubScopedBPart
        {
            [MefV2.Import]
            public RootScopeSecondPart Root { get; set; }
        }

        #endregion

    }
}

#endif
