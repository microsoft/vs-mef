namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Microsoft.VisualStudio.Composition.BrokenAssemblyTests;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class RejectionTests
    {
        /// <summary>
        /// Verifies that even though an arguably defective part is imported with AllowDefault=true,
        /// the importing part is rejected.
        /// </summary>
        /// <remarks>
        /// At first it seems it's the exporting part that should be rejected. But because the VS editor
        /// and other folks sometimes use this pattern to export metadata into a catalog without an intention
        /// of the part itself every providing legitimate exports, we have to allow *lazy* imports to work
        /// with these semi-defective parts.
        /// </remarks>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3AllowConfigurationWithErrors, typeof(SomePartWithoutImportingConstructor), typeof(PartThatOptionallyImportsBrokenPart), InvalidConfiguration = true)]
        public void OptionalImportOfPartWithMissingImportingConstructorV1Compat(IContainer container)
        {
            var exports = container.GetExportedValues<PartThatOptionallyImportsBrokenPart>().ToList();

            // We only reach this assertion in V3. The other two engines have already thrown
            // an exception from the first line.
            // The V1 attribute reader is content to produce parts without importing constructors,
            // so when someone imports the faulty part (optionally or otherwise) it rejects the importing part.
            Assert.Equal(0, exports.Count);
        }

        [MefFact(CompositionEngines.V2Compat | CompositionEngines.V3AllowConfigurationWithErrors, typeof(SomePartWithoutImportingConstructor), typeof(PartThatOptionallyImportsBrokenPart), InvalidConfiguration = true)]
        public void OptionalImportOfPartWithMissingImportingConstructorV2Compat(IContainer container)
        {
            // V2 throws here
            var exports = container.GetExportedValues<PartThatOptionallyImportsBrokenPart>().ToList();

            // In V3 (with V2 attribute reader) we have rejected the part with the missing importing constructor.
            // So the optional import of the rejected type is fine to remain unsatisfied.
            Assert.Equal(1, exports.Count);
        }

        [MefFact(CompositionEngines.V2, typeof(SomePartWithoutImportingConstructor), typeof(PartThatUnconditionallyLazyImportsBrokenPart), InvalidConfiguration = true, NoCompatGoal = true)]
        public void LazyImportOfPartWithMissingImportingConstructorV2(IContainer container)
        {
            container.GetExportedValues<PartThatUnconditionallyLazyImportsBrokenPart>();
        }

        /// <summary>
        /// Verifies that a MEFv1 trick of exporting metadata on a non-instantiable part works.
        /// </summary>
        /// <remarks>
        /// MEFv2 always requires an importing constructor so we don't include that in the test matrix for this method.
        /// </remarks>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3AllowConfigurationWithErrors, typeof(SomePartWithoutImportingConstructor), typeof(PartThatUnconditionallyLazyImportsBrokenPart), InvalidConfiguration = true)]
        public void LazyImportOfPartWithMissingImportingConstructor(IContainer container)
        {
            var exports = container.GetExportedValues<PartThatUnconditionallyLazyImportsBrokenPart>().ToList();

            Assert.Equal(1, exports.Count);
            Assert.NotNull(exports[0].ImportOfBrokenPart);

            // It should finally fail here.
            try
            {
                var expectFailure = exports[0].ImportOfBrokenPart.Value;
                Assert.False(true, "Some type of composition exception was expected here.");
            }
            catch (CompositionFailedException)
            {
                // For V3 our test harness is not expecting us to throw.
            }
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat | CompositionEngines.V3AllowConfigurationWithErrors, typeof(SomePartWithoutImportingConstructor), typeof(PartThatUnconditionallyImportsBrokenPart), InvalidConfiguration = true)]
        public void RequiredImportOfPartWithMissingImportingConstructor(IContainer container)
        {
            var exports = container.GetExportedValues<PartThatUnconditionallyImportsBrokenPart>().ToList();

            // We only reach this assertion in V3. The other two engines have already thrown
            // an exception from the first line.
            Assert.Equal(0, exports.Count);
        }

        [MefV1.Export, Export]
        public class SomePartWithoutImportingConstructor
        {
            public SomePartWithoutImportingConstructor(int foo) { }
        }

        [MefV1.Export, Export]
        public class PartThatOptionallyImportsBrokenPart
        {
            [MefV1.Import(AllowDefault = true), Import(AllowDefault = true)]
            public SomePartWithoutImportingConstructor ImportOfBrokenPart { get; set; }
        }

        [MefV1.Export, Export]
        public class PartThatUnconditionallyImportsBrokenPart
        {
            [MefV1.Import, Import]
            public SomePartWithoutImportingConstructor ImportOfBrokenPart { get; set; }
        }

        [MefV1.Export, Export]
        public class PartThatUnconditionallyLazyImportsBrokenPart
        {
            [MefV1.Import, Import]
            public Lazy<SomePartWithoutImportingConstructor> ImportOfBrokenPart { get; set; }
        }
    }
}
