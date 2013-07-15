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
        [MefFact(CompositionEngines.V1, new Type[0])]
        public void GetExportsOfICompositionService(IContainer container)
        {
            var service = container.GetExportedValue<ICompositionService>();
            Assert.NotNull(service);
        }

        [MefFact(CompositionEngines.V1, typeof(CompositionServiceImportingPart))]
        public void ImportCompositionService(IContainer container)
        {
            var part = container.GetExportedValue<CompositionServiceImportingPart>();
            Assert.NotNull(part.CompositionService);
        }

        [Fact(Skip = "Not yet implemented.")]
        public void CompositionContainerImplementsICompositionService()
        {
            Assert.True(typeof(ICompositionService).IsAssignableFrom(typeof(CompositionContainer)));
        }

        /// <summary>
        /// Verifies that SatisfyImportsOnce functions correctly with an object
        /// that was included during catalog discovery with Import attributes on its members.
        /// </summary>
        [MefFact(CompositionEngines.V1, typeof(CompositionServiceImportingPart), typeof(ImportOnlyPart))]
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
        [MefFact(CompositionEngines.V1, typeof(CompositionServiceImportingPart))] // intentionally leaves out ImportOnlyPart
        public void SatisfyImportsOnceWithUnknownImportOnlyPart(IContainer container)
        {
            var exportedPart = container.GetExportedValue<CompositionServiceImportingPart>();
            ICompositionService compositionService = exportedPart.CompositionService;

            var value = new ImportOnlyPart();
            compositionService.SatisfyImportsOnce(value);
            Assert.NotNull(value.SomePropertyThatImports);
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
