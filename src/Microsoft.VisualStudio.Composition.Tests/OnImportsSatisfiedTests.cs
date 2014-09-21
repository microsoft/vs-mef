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

    [Trait("IPartImportsSatisfiedNotification", "")]
    public class OnImportsSatisfiedTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SpecialPart), typeof(SomeRandomPart))]
        public void OnImportsSatisfied(IContainer container)
        {
            var part = container.GetExportedValue<SpecialPart>();
            Assert.Equal(1, part.ImportsSatisfiedInvocationCount);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat, typeof(SpecialPartInternal), typeof(SomeRandomPart))]
        public void OnImportsSatisfiedInternalPart(IContainer container)
        {
            var part = container.GetExportedValue<SpecialPartInternal>();
            Assert.Equal(1, part.ImportsSatisfiedInvocationCount);
        }

        [MefV1.Export, Export]
        public class SpecialPart : MefV1.IPartImportsSatisfiedNotification
        {
            public int ImportsSatisfiedInvocationCount { get; set; }

            [Import, MefV1.Import]
            public SomeRandomPart SomeImport { get; set; }

            [OnImportsSatisfied] // V2
            public void ImportsSatisfied()
            {
                this.ImportsSatisfiedInvocationCount++;
                Assert.NotNull(this.SomeImport);
            }

            // V1. We're using explicit implementation syntax deliberately as part of the test.
            void MefV1.IPartImportsSatisfiedNotification.OnImportsSatisfied()
            {
                this.ImportsSatisfiedInvocationCount++;
                Assert.NotNull(this.SomeImport);
            }
        }

        [MefV1.Export]
        internal class SpecialPartInternal : MefV1.IPartImportsSatisfiedNotification
        {
            public int ImportsSatisfiedInvocationCount { get; set; }

            [MefV1.Import]
            public SomeRandomPart SomeImport { get; set; }

            // V1. We're using explicit implementation syntax deliberately as part of the test.
            void MefV1.IPartImportsSatisfiedNotification.OnImportsSatisfied()
            {
                this.ImportsSatisfiedInvocationCount++;
                Assert.NotNull(this.SomeImport);
            }
        }

        [MefV1.Export, Export]
        public class SomeRandomPart { }
    }
}
