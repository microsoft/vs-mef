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

    public class InvalidGraphTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat | CompositionEngines.V3AllowConfigurationWithErrors,
            typeof(PartWithMissingImport), typeof(PartWithSatisfiedImports), typeof(SomePart))]
        public void UnsatisfiedImportRemovesPartAndRetainsUnrelatedParts(IContainer container)
        {
            var satisfiedPart = container.GetExportedValue<PartWithSatisfiedImports>();
            Assert.NotNull(satisfiedPart.SatisfiedImport);

            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PartWithMissingImport>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
            typeof(PartThatOptionallyImportsPartWithMissingImport), typeof(PartThatRequiresPartWithMissingImport), typeof(PartWithMissingImport), typeof(PartWithSatisfiedImports), typeof(SomePart))]
        public void UnsatisfiedImportRemovesPartAndRequiringDependentsAndRetainsSalvageableParts(IContainer container)
        {
            var satisfiedPart = container.GetExportedValue<PartWithSatisfiedImports>();
            Assert.NotNull(satisfiedPart.SatisfiedImport);

            var optionalImportPart = container.GetExportedValue<PartThatOptionallyImportsPartWithMissingImport>();
            Assert.Null(optionalImportPart.ImportOfPartWithMissingImport);

            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PartWithMissingImport>());
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PartThatRequiresPartWithMissingImport>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat | CompositionEngines.V3AllowConfigurationWithErrors,
            typeof(PartThatRequiresPartWithMissingImport), typeof(PartWithMissingImport), typeof(PartWithSatisfiedImports), typeof(SomePart))]
        public void UnsatisfiedImportRemovesPartAndRequiringDependentsAndRetainsUnrelatedParts(IContainer container)
        {
            var satisfiedPart = container.GetExportedValue<PartWithSatisfiedImports>();
            Assert.NotNull(satisfiedPart.SatisfiedImport);

            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PartWithMissingImport>());
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<PartThatRequiresPartWithMissingImport>());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat | CompositionEngines.V3AllowConfigurationWithErrors,
            typeof(PartThatRequiresPartWithMissingImport), typeof(PartWithMissingImport), typeof(PartWithSatisfiedImports), typeof(SomePart))]
        public void CompositionErrors_HasMultiLevelErrors(IContainer container)
        {
            var satisfiedPart = container.GetExportedValue<PartWithSatisfiedImports>();
            Assert.NotNull(satisfiedPart.SatisfiedImport);

            var v3Container = container as TestUtilities.V3ContainerWrapper;
            if (v3Container != null)
            {
                var rootCauses = v3Container.Configuration.CompositionErrors.Peek();
                var secondOrder = v3Container.Configuration.CompositionErrors.Pop().Peek();
                Assert.Equal(typeof(PartWithMissingImport), rootCauses.Single().Parts.Single().Definition.Type);
                Assert.Equal(typeof(PartThatRequiresPartWithMissingImport), secondOrder.Single().Parts.Single().Definition.Type);
            }
        }

        [Export, MefV1.Export]
        public class PartWithMissingImport
        {
            [Import, MefV1.Import]
            public IFormattable MissingImportProperty { get; set; }
        }

        [Export, MefV1.Export]
        public class PartThatRequiresPartWithMissingImport
        {
            [Import, MefV1.Import]
            public PartWithMissingImport ImportOfPartWithMissingImport { get; set; }
        }

        [Export, MefV1.Export]
        public class PartThatOptionallyImportsPartWithMissingImport
        {
            [Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]
            public PartWithMissingImport ImportOfPartWithMissingImport { get; set; }
        }

        [Export, MefV1.Export]
        public class PartWithSatisfiedImports
        {
            [Import, MefV1.Import]
            public SomePart SatisfiedImport { get; set; }
        }

        [Export, MefV1.Export]
        public class SomePart { }

        #region Uncreatable part

        /// <summary>
        /// Verifies that all MEF versions reject non-lazy imports of non-static exports from parts that lack importing constructors.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat | CompositionEngines.V3AllowConfigurationWithErrors,
            typeof(UncreatablePart), typeof(PartThatImportsUncreatablePart))]
        public void UncreatableImportedPart(IContainer container)
        {
            var v3Container = container as TestUtilities.V3ContainerWrapper;
            if (v3Container != null)
            {
                Assert.False(v3Container.Configuration.CompositionErrors.IsEmpty);
                var rootCauses = v3Container.Configuration.CompositionErrors.Peek();
                Assert.Equal(typeof(PartThatImportsUncreatablePart), rootCauses.Single().Parts.Single().Definition.Type);
            }

            try
            {
                container.GetExportedValue<PartThatImportsUncreatablePart>();
            }
            catch (CompositionFailedException) { }
            catch (MefV1.CompositionException) { }
        }

        /// <summary>
        /// Verifies that V1 lets folks get away with importing parts that lack importing constructors 
        /// as long as they import it lazily, all the way to the point of evaluating the lazy.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat, typeof(UncreatablePart), typeof(PartThatLazyImportsUncreatablePart))]
        public void UncreatableLazyImportedPartV1(IContainer container)
        {
            var part = container.GetExportedValue<PartThatLazyImportsUncreatablePart>();
            Assert.NotNull(part.ImportOfUncreatablePart);
            Assert.NotNull(part.ImportOfUncreatablePart.Metadata);
            try
            {
                var throws = part.ImportOfUncreatablePart.Value;
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (CompositionFailedException) { }
            catch (MefV1.CompositionException) { }
        }

        /// <summary>
        /// Verifies that V2 always rejects parts lacking importing constructors.
        /// </summary>
        [MefFact(CompositionEngines.V2Compat, typeof(UncreatablePart), typeof(PartThatLazyImportsUncreatablePart), InvalidConfiguration = true)]
        public void UncreatableLazyImportedPartV2(IContainer container)
        {
            container.GetExportedValue<PartThatLazyImportsUncreatablePart>();
        }

        [Export, MefV1.Export]
        public class UncreatablePart
        {
            public UncreatablePart(IServiceProvider serviceProvider) { }
        }

        [Export, MefV1.Export]
        public class PartThatImportsUncreatablePart
        {
            [Import, MefV1.Import]
            public UncreatablePart ImportOfUncreatablePart { get; set; }
        }

        [Export, MefV1.Export]
        public class PartThatLazyImportsUncreatablePart
        {
            [Import, MefV1.Import]
            public Lazy<UncreatablePart, IDictionary<string, object>> ImportOfUncreatablePart { get; set; }
        }

        #endregion
    }
}
