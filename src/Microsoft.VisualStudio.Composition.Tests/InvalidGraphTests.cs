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

        #region Uncreatable part with import

        /// <summary>
        /// Verifies that V1 lets folks get away with importing parts that lack importing constructors 
        /// as long as they import it lazily, all the way to the point of evaluating the lazy.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat, typeof(SomePart), typeof(UncreatablePartWithImportingProperty), typeof(PartThatLazilyImportsUncreatablePartWithImportingProperty))]
        public void UncreatableLazyImportedPartWithImportingPropertyV1(IContainer container)
        {
            var part = container.GetExportedValue<PartThatLazilyImportsUncreatablePartWithImportingProperty>();
            Assert.NotNull(part.LazyImportOfUncreatablePart);
            Assert.NotNull(part.LazyImportOfUncreatablePart.Metadata);
            try
            {
                var throws = part.LazyImportOfUncreatablePart.Value;
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (CompositionFailedException) { }
            catch (MefV1.CompositionException) { }
        }

        [Export, MefV1.Export]
        public class UncreatablePartWithImportingProperty
        {
            public UncreatablePartWithImportingProperty(IServiceProvider serviceProvider) { }

            [Import, MefV1.Import]
            public SomePart ImportingProperty { get; set; }
        }

        [Export, MefV1.Export]
        public class PartThatLazilyImportsUncreatablePartWithImportingProperty
        {
            [Import, MefV1.Import]
            public Lazy<UncreatablePartWithImportingProperty, IDictionary<string, object>> LazyImportOfUncreatablePart { get; set; }
        }

        #endregion

        #region Exporting an interface that is not implemented

        // CONSIDER: Add tests where the exporting property/field is typed such that it can be statically determined
        //           that a failure at runtime is inevitable. For example an Int32 property that exports a String.
        //           Be careful though, because in VS there *are* cases (in Sharepoint IIRC) where incompatible types
        //           are impossibly exported, but because they are never Imported directly (only through very carefully
        //           written calls to the ExportProvider to type the exports as "object") it doesn't fail at runtime.
        //           We may need to keep that working (at least until we can talk Sharepoint out of doing it).

        [MefFact(CompositionEngines.V1, typeof(PartWithObjectPropertyExportedAsIComparable), typeof(PartThatImportsIComparable))]
        public void ExportingProperty_FailsAtRuntime(IContainer container)
        {
            try
            {
                container.GetExportedValue<PartThatImportsIComparable>();
            }
            catch (CompositionFailedException ex)
            {
                // We also want to ensure that the exception message points at the guilty party.
                Assert.True(ex.Message.Contains(typeof(PartWithObjectPropertyExportedAsIComparable).Name));
            }
        }

        /// <summary>
        /// Verifies that MEF permits an exporting property to export contract types
        /// if the returned value implements the exported contract type, even if the type
        /// of the property itself doesn't guarantee that it would succeed at runtime.
        /// </summary>
        [MefFact(CompositionEngines.V1, typeof(PartShouldExportValidValue), typeof(PartWithObjectPropertyExportedAsIComparable), typeof(PartThatImportsIComparable))]
        public void ExportingProperty_SucceedsAtRuntime(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsIComparable>();
            Assert.Same(PartWithObjectPropertyExportedAsIComparable.ComparableValue, part.ComparableImport);
            Assert.Same(PartWithObjectPropertyExportedAsIComparable.ComparableValue, part.ComparableImportManyArray[0]);
            Assert.Same(PartWithObjectPropertyExportedAsIComparable.ComparableValue, part.ComparableImportManyList[0]);
        }

        /// <summary>
        /// Verifies that MEF permits an exporting field to export contract types
        /// if the returned value implements the exported contract type, even if the type
        /// of the field itself doesn't guarantee that it would succeed at runtime.
        /// </summary>
        [MefFact(CompositionEngines.V1, typeof(PartShouldExportValidValue), typeof(PartWithObjectFieldExportedAsIComparable), typeof(PartThatImportsIComparable))]
        public void ExportingField_SucceedsAtRuntime(IContainer container)
        {
            var part = container.GetExportedValue<PartThatImportsIComparable>();
            Assert.Same(PartWithObjectPropertyExportedAsIComparable.ComparableValue, part.ComparableImport);
            Assert.Same(PartWithObjectPropertyExportedAsIComparable.ComparableValue, part.ComparableImportManyArray[0]);
            Assert.Same(PartWithObjectPropertyExportedAsIComparable.ComparableValue, part.ComparableImportManyList[0]);
        }

        [MefFact(CompositionEngines.V1, typeof(PartWithObjectFieldExportedAsIComparable), typeof(PartThatImportsIComparable))]
        public void ExportingField_FailsAtRuntime(IContainer container)
        {
            try
            {
                var part = container.GetExportedValue<PartThatImportsIComparable>();
            }
            catch (CompositionFailedException ex)
            {
                // We also want to ensure that the exception message points at the guilty party.
                Assert.True(ex.Message.Contains(typeof(PartWithObjectFieldExportedAsIComparable).Name));
            }
        }

        /// <summary>
        /// Verifies that MEF recognizes exporting a type not implemented by an exported type as an error.
        /// </summary>
        /// <remarks>
        /// When a type itself has an export on it, we know by static analysis whether the value
        /// (the instantiated type) implements the exported type. MEF should produce an error when that occurs.
        /// </remarks>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartExportingIComparableAsTypeWithoutImplementing), typeof(PartThatImportsIComparable), InvalidConfiguration = true)]
        public void ExportingTypeFailsAtCompositionTime(IContainer container)
        {
            container.GetExportedValue<PartThatImportsIComparable>();
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartShouldExportValidValue { }

        public class PartWithObjectPropertyExportedAsIComparable
        {
            internal static readonly object ComparableValue = "strings are comparable";

            [Import(AllowDefault = true), MefV1.Import(AllowDefault = true)]
            public PartShouldExportValidValue ShouldExportValidValue { get; set; }

            [Export(typeof(IComparable)), MefV1.Export(typeof(IComparable))]
            public object SomeExportedValue
            {
                get
                {
                    if (this.ShouldExportValidValue != null)
                    {
                        return ComparableValue;
                    }
                    else
                    {
                        // Return a value that does not implement the exported interface.
                        // This is interesting to test for because MEF cannot tell what actual
                        // reference type will be returned at runtime from this property so
                        // it cannot know that the interface will not be implemented.
                        // So this tests how MEF deals with the failure at runtime.
                        return new object();
                    }
                }
            }
        }

        public class PartWithObjectFieldExportedAsIComparable
        {
            internal static readonly object ComparableValue = "strings are comparable";

            [MefV1.ImportingConstructor]
            public PartWithObjectFieldExportedAsIComparable([MefV1.Import(AllowDefault = true)] PartShouldExportValidValue shouldExportValidValue)
            {
                if (shouldExportValidValue != null)
                {
                    this.SomeExportedValue = ComparableValue;
                }
                else
                {
                    // Return a value that does not implement the exported interface.
                    // This is interesting to test for because MEF cannot tell what actual
                    // reference type will be returned at runtime from this property so
                    // it cannot know that the interface will not be implemented.
                    // So this tests how MEF deals with the failure at runtime.
                    this.SomeExportedValue = new object();
                }
            }

            [MefV1.Export(typeof(IComparable))]
            public object SomeExportedValue;
        }

        [Export(typeof(IComparable)), MefV1.Export(typeof(IComparable))]
        public class PartExportingIComparableAsTypeWithoutImplementing { }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsIComparable
        {
            [Import, MefV1.Import]
            public IComparable ComparableImport { get; set; }

            [ImportMany, MefV1.ImportMany]
            public IComparable[] ComparableImportManyArray { get; set; }

            [ImportMany, MefV1.ImportMany]
            public List<IComparable> ComparableImportManyList { get; set; }
        }

        #endregion
    }
}
