// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ImportSiteCreationPolicyTests
    {
        #region Import of PartCreationPolicy.Any tests and parts

        [MefFact(CompositionEngines.V1Compat, typeof(ImportAnyAsAnyPart), typeof(ExportWithAnyCreationPolicy), typeof(ExportWithDefaultCreationPolicy))]
        public void ImportAnyAsAny(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportAnyAsAnyPart>();
            var part2 = container.GetExportedValue<ImportAnyAsAnyPart>();
            Assert.Same(part1.ImportAnyAsAny, part2.ImportAnyAsAny);
            Assert.Same(part1.ImportAnyAsDefault, part2.ImportAnyAsDefault);
            Assert.Same(part1.ImportDefaultAsAny, part2.ImportDefaultAsAny);
            Assert.Same(part1.ImportDefaultAsDefault, part2.ImportDefaultAsDefault);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportAnyAsSharedPart), typeof(ExportWithAnyCreationPolicy))]
        public void ImportAnyAsShared(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportAnyAsSharedPart>();
            var part2 = container.GetExportedValue<ImportAnyAsSharedPart>();
            Assert.Same(part1.ImportingProperty, part2.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportAnyAsNonSharedPart), typeof(ExportWithAnyCreationPolicy))]
        public void ImportAnyAsNonShared(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportAnyAsNonSharedPart>();
            var part2 = container.GetExportedValue<ImportAnyAsNonSharedPart>();
            Assert.NotSame(part1.ImportingProperty, part2.ImportingProperty);
        }

        [Export]
        public class ExportWithDefaultCreationPolicy { }

        [Export, PartCreationPolicy(CreationPolicy.Any)]
        public class ExportWithAnyCreationPolicy { }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportAnyAsAnyPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithAnyCreationPolicy ImportAnyAsAny { get; set; } = null!;

            [Import]
            public ExportWithAnyCreationPolicy ImportAnyAsDefault { get; set; } = null!;

            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithDefaultCreationPolicy ImportDefaultAsAny { get; set; } = null!;

            [Import]
            public ExportWithDefaultCreationPolicy ImportDefaultAsDefault { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportAnyAsSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithAnyCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportAnyAsNonSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithAnyCreationPolicy ImportingProperty { get; set; } = null!;
        }

        #endregion

        #region Import of PartCreationPolicy.Shared tests and parts

        [MefFact(CompositionEngines.V1Compat, typeof(ImportSharedAsAnyPart), typeof(ExportWithSharedCreationPolicy))]
        public void ImportSharedAsAny(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportSharedAsAnyPart>();
            var part2 = container.GetExportedValue<ImportSharedAsAnyPart>();
            Assert.Same(part1.ImportingProperty, part2.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportSharedAsSharedPart), typeof(ExportWithSharedCreationPolicy))]
        public void ImportSharedAsShared(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportSharedAsSharedPart>();
            var part2 = container.GetExportedValue<ImportSharedAsSharedPart>();
            Assert.Same(part1.ImportingProperty, part2.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportSharedAsNonSharedPart), typeof(ExportWithSharedCreationPolicy), InvalidConfiguration = true)]
        public void ImportSharedAsNonShared(IContainer container)
        {
            container.GetExportedValue<ImportSharedAsNonSharedPart>();
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportSharedAsNonSharedOptionalPart), typeof(ExportWithSharedCreationPolicy))]
        public void ImportSharedAsNonSharedOptional(IContainer container)
        {
            var part = container.GetExportedValue<ImportSharedAsNonSharedOptionalPart>();
            Assert.Null(part.ImportingProperty);
        }

        [Export, PartCreationPolicy(CreationPolicy.Shared)]
        [Export(typeof(object))]
        public class ExportWithSharedCreationPolicy { }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsNonSharedPart // Invalid combination test
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsNonSharedOptionalPart
        {
            [Import(AllowDefault = true, RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsAnyPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        #endregion

        #region Import of PartCreationPolicy.NonShared parts

        [MefFact(CompositionEngines.V1Compat, typeof(ImportNonSharedAsAnyPart), typeof(ExportWithNonSharedCreationPolicy))]
        public void ImportNonSharedAsAny(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportNonSharedAsAnyPart>();
            var part2 = container.GetExportedValue<ImportNonSharedAsAnyPart>();
            Assert.NotSame(part1.ImportingProperty, part2.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportNonSharedAsSharedPart), typeof(ExportWithNonSharedCreationPolicy), InvalidConfiguration = true)]
        public void ImportNonSharedAsShared(IContainer container)
        {
            container.GetExportedValue<ImportNonSharedAsSharedPart>();
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportNonSharedAsSharedOptionalPart), typeof(ExportWithNonSharedCreationPolicy))]
        public void ImportNonSharedAsSharedOptional(IContainer container)
        {
            var part = container.GetExportedValue<ImportNonSharedAsSharedOptionalPart>();
            Assert.Null(part.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportNonSharedAsNonSharedPart), typeof(ExportWithNonSharedCreationPolicy))]
        public void ImportNonSharedAsNonShared(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportNonSharedAsNonSharedPart>();
            var part2 = container.GetExportedValue<ImportNonSharedAsNonSharedPart>();
            Assert.NotSame(part1.ImportingProperty, part2.ImportingProperty);
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        [Export(typeof(object))]
        public class ExportWithNonSharedCreationPolicy { }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsSharedPart // Invalid combination test
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsSharedOptionalPart
        {
            [Import(AllowDefault = true, RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsNonSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsAnyPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; } = null!;
        }

        #endregion

        #region Filtering tests

        [MefFact(CompositionEngines.V1Compat, typeof(ImportExportFactoryWithFilteringExportsPart), typeof(ExportWithSharedCreationPolicy), typeof(ExportWithNonSharedCreationPolicy))]
        [Trait("ExportFactory", "")]
        public void CreationPolicyFiltersExportFactory(IContainer container)
        {
            var factory = container.GetExportedValue<ImportExportFactoryWithFilteringExportsPart>();
            var export = factory.Factory.CreateExport();
            Assert.IsType<ExportWithNonSharedCreationPolicy>(export.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportOneWithFilteringExportsPart), typeof(ExportWithSharedCreationPolicy), typeof(ExportWithNonSharedCreationPolicy))]
        public void CreationPolicyFiltersImportOne(IContainer container)
        {
            var part = container.GetExportedValue<ImportOneWithFilteringExportsPart>();
            Assert.IsType<ExportWithNonSharedCreationPolicy>(part.NonShared);
            Assert.IsType<ExportWithSharedCreationPolicy>(part.Shared);
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportExportFactoryWithFilteringExportsPart
        {
            [Import]
            public ExportFactory<object> Factory { get; set; } = null!;
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportOneWithFilteringExportsPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public object NonShared { get; set; } = null!;

            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public object Shared { get; set; } = null!;
        }

        #endregion
    }
}
