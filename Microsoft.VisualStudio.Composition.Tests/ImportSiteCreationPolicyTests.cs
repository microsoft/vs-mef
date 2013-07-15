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

        [MefFact(CompositionEngines.V1, typeof(ImportAnyAsNonSharedPart), typeof(ExportWithAnyCreationPolicy))]
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
            public ExportWithAnyCreationPolicy ImportAnyAsAny { get; set; }

            [Import]
            public ExportWithAnyCreationPolicy ImportAnyAsDefault { get; set; }

            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithDefaultCreationPolicy ImportDefaultAsAny { get; set; }

            [Import]
            public ExportWithDefaultCreationPolicy ImportDefaultAsDefault { get; set; }
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportAnyAsSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithAnyCreationPolicy ImportingProperty { get; set; }
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportAnyAsNonSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithAnyCreationPolicy ImportingProperty { get; set; }
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

        [MefFact(CompositionEngines.V1, typeof(ImportSharedAsNonSharedPart), typeof(ExportWithSharedCreationPolicy), InvalidConfiguration = true)]
        public void ImportSharedAsNonShared(IContainer container)
        {
            container.GetExportedValue<ImportSharedAsNonSharedPart>();
        }

        [Export, PartCreationPolicy(CreationPolicy.Shared)]
        public class ExportWithSharedCreationPolicy { }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsNonSharedPart // Invalid combination test
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; }
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; }
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportSharedAsAnyPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithSharedCreationPolicy ImportingProperty { get; set; }
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

        [MefFact(CompositionEngines.V1, typeof(ImportNonSharedAsSharedPart), typeof(ExportWithNonSharedCreationPolicy), InvalidConfiguration = true)]
        public void ImportNonSharedAsShared(IContainer container)
        {
            container.GetExportedValue<ImportNonSharedAsSharedPart>();
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ImportNonSharedAsNonSharedPart), typeof(ExportWithNonSharedCreationPolicy))]
        public void ImportNonSharedAsNonShared(IContainer container)
        {
            var part1 = container.GetExportedValue<ImportNonSharedAsNonSharedPart>();
            var part2 = container.GetExportedValue<ImportNonSharedAsNonSharedPart>();
            Assert.NotSame(part1.ImportingProperty, part2.ImportingProperty);
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ExportWithNonSharedCreationPolicy { }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsSharedPart // Invalid combination test
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Shared)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; }
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsNonSharedPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.NonShared)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; }
        }

        [Export, PartCreationPolicy(CreationPolicy.NonShared)]
        public class ImportNonSharedAsAnyPart
        {
            [Import(RequiredCreationPolicy = CreationPolicy.Any)]
            public ExportWithNonSharedCreationPolicy ImportingProperty { get; set; }
        }

        #endregion
    }
}
