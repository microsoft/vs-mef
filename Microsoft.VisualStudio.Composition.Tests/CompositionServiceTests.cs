namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

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

        [MefFact(CompositionEngines.V1Compat, Skip = "Test not yet implemented.")]
        public void CompositionServiceSatisfiesWithExportsFromAppropriateScope(IContainer container)
        {
            // Given a configuration where there are sub-scopes (i.e. sharing boundaries), whatever
            // the scope is that imports an ICompositionService should determine what exports are
            // available to satisfy imports passed into it.
            // TODO: Code here
        }

        [Fact]
        public void AddCompositionServiceToCatalogTwice()
        {
            var catalog = ComposableCatalog.Create();
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
    }
}
