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

    /// <summary>
    /// Tests V3-specific behavior that one can import the V3 ExportProvider at their sharing boundary.
    /// </summary>
    public class ImportedExportProviderTests
    {
        #region Simple tests with just one sharing scope

        [MefFact(CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1, typeof(PartThatImportsExportProvider))]
        public void CanImportExportProvider(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            Assert.NotNull(importer.ExportProvider);
        }

        [MefFact(CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1, typeof(PartThatImportsExportProvider), typeof(SomeOtherPart))]
        public void CanAcquireExportsViaImportedExportProvider(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            var otherPart = importer.ExportProvider.GetExportedValue<SomeOtherPart>();
            Assert.NotNull(otherPart);
        }

        [MefFact(CompositionEngines.V3EmulatingV2 | CompositionEngines.V3EmulatingV1, typeof(PartThatImportsExportProvider))]
        public void ExportProviderCanAcquireSameExportProvider(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            Assert.Same(importer.ExportProvider, importer.ExportProvider.GetExportedValue<ExportProvider>());
        }

        /// <summary>
        /// Verifies that parts cannot dispose of their owners.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV2, typeof(PartThatImportsExportProvider))]
        public void ImportedExportProviderCannotBeDisposed(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            Assert.Throws<InvalidOperationException>(() => importer.ExportProvider.Dispose());
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsExportProvider
        {
            [Import, MefV1.Import]
            public ExportProvider ExportProvider { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class SomeOtherPart { }

        #endregion

        #region Tests for imported ExportProvider offering values at the appropriate scope

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(TopLevelPart), typeof(SubScopePart))]
        public void TopLevelExportProviderCannotConstructSubScopedParts(IContainer container)
        {
            var topLevel = container.GetExportedValue<TopLevelPart>();
            Assert.Throws<CompositionFailedException>(() => topLevel.ExportProvider.GetExportedValue<SubScopePart>());
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(TopLevelPart), typeof(SubScopePart), typeof(OtherSubScopePart))]
        public void SubScopedExportProviderCanRetrieveExportsInSameScope(IContainer container)
        {
            var topLevel = container.GetExportedValue<TopLevelPart>();
            var subScopePart = topLevel.ScopeFactory.CreateExport().Value;
            var otherPart = subScopePart.ExportProvider.GetExportedValue<OtherSubScopePart>();
            Assert.NotNull(otherPart);
            Assert.Same(otherPart, subScopePart.OtherPart);
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(TopLevelPart), typeof(SubScopePart), typeof(OtherSubScopePart))]
        public void SubScopedExportProviderCanRetrieveExportsInParentScopes(IContainer container)
        {
            var topLevel = container.GetExportedValue<TopLevelPart>();
            var subScopePart = topLevel.ScopeFactory.CreateExport().Value;
            var retrievedTopLevel = subScopePart.ExportProvider.GetExportedValue<TopLevelPart>();
            Assert.Same(topLevel, retrievedTopLevel);
        }

        [Export, Shared]
        public class TopLevelPart
        {
            [Import, SharingBoundary("SubScope")]
            public ExportFactory<SubScopePart> ScopeFactory { get; set; }

            [Import]
            public ExportProvider ExportProvider { get; set; }
        }

        [Export, Shared("SubScope")]
        public class SubScopePart
        {
            [Import]
            public ExportProvider ExportProvider { get; set; }

            [Import(AllowDefault = true)]
            public OtherSubScopePart OtherPart { get; set; }
        }

        [Export, Shared]
        public class OtherSubScopePart
        {
            [Import]
            public ExportProvider ExportProvider { get; set; }
        }

        #endregion
    }
}
