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
            Assert.Same(importer.ExportProvider, importer.ExportProvider.GetExportedValues<ExportProvider>().Single());
        }

        /// <summary>
        /// Verifies that parts cannot dispose of their owners.
        /// </summary>
        [Trait("Disposal", "")]
        [MefFact(CompositionEngines.V3EmulatingV2, typeof(PartThatImportsExportProvider))]
        public void ImportedExportProviderCannotBeDisposed(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsExportProvider>();
            Assert.Throws<InvalidOperationException>(() => importer.ExportProvider.Dispose());
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(PartThatExportsExportProvider), InvalidConfiguration = true)]
        public void CannotExportExportProvider(IContainer container)
        {
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsExportProvider
        {
            [Import, MefV1.Import]
            public ExportProvider ExportProvider { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class SomeOtherPart { }

        public class PartThatExportsExportProvider
        {
            [Export]
            [MefV1.Export]
            public ExportProvider? ExportProvider
            {
                get { return null; }
            }
        }

        #endregion

        #region Tests for imported ExportProvider offering values at the appropriate scope

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(TopLevelPart), typeof(SubScopePart))]
        public void TopLevelExportProviderCannotConstructSubScopedParts(IContainer container)
        {
            var topLevel = container.GetExportedValue<TopLevelPart>();
            Assert.Throws<CompositionFailedException>(() => topLevel.ExportProvider.GetExportedValue<SubScopePart>());
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(TopLevelPart), typeof(SubScopePart))]
        public void TopLevelExportProviderIsNotSameAsSubScopeExportProvider(IContainer container)
        {
            var topLevel = container.GetExportedValue<TopLevelPart>();
            var subScopePart = topLevel.ScopeFactory.CreateExport().Value;
            Assert.NotSame(topLevel.ExportProvider, subScopePart.ExportProvider);
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
            public ExportFactory<SubScopePart> ScopeFactory { get; set; } = null!;

            [Import]
            public ExportProvider ExportProvider { get; set; } = null!;
        }

        [Export, Shared("SubScope")]
        public class SubScopePart
        {
            [Import]
            public ExportProvider ExportProvider { get; set; } = null!;

            [Import(AllowDefault = true)]
            public OtherSubScopePart OtherPart { get; set; } = null!;
        }

        [Export, Shared("SubScope")]
        public class OtherSubScopePart
        {
            [Import]
            public ExportProvider ExportProvider { get; set; } = null!;
        }

        #endregion

        #region Test for ImportMany on ExportProvider

        [MefFact(CompositionEngines.V3EmulatingV1, typeof(PartWithImportManyExportProvider))]
        public void ImportManyExportProvider(IContainer container)
        {
            var part = container.GetExportedValue<PartWithImportManyExportProvider>();

            Assert.NotNull(part.ExportProvidersPublicList);
            Assert.Equal(1, part.ExportProvidersPublicList.Count);
            Assert.NotNull(part.ExportProvidersPublicList[0]);

            Assert.NotNull(part.ExportProvidersPublicArray);
            Assert.Equal(1, part.ExportProvidersPublicArray.Length);
            Assert.Same(part.ExportProvidersPublicList[0], part.ExportProvidersPublicArray[0]);

            Assert.NotNull(part.ExportProvidersInternalArray);
            Assert.Equal(1, part.ExportProvidersInternalArray.Length);
            Assert.Same(part.ExportProvidersPublicList[0], part.ExportProvidersInternalArray[0]);
        }

        [MefV1.Export]
        public class PartWithImportManyExportProvider
        {
            [MefV1.ImportMany]
            public List<ExportProvider> ExportProvidersPublicList { get; set; } = null!;

            [MefV1.ImportMany]
            public ExportProvider[] ExportProvidersPublicArray { get; set; } = null!;

            [MefV1.ImportMany]
            internal ExportProvider[] ExportProvidersInternalArray { get; set; } = null!;
        }

        #endregion
    }
}
