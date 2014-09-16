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

    public class GenericImportTests
    {
        /// <summary>
        /// This is a very difficult scenario to support in MEFv3, since it means much of the graph
        /// could change based on the type argument. We may never be able to support it.
        /// </summary>
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, NoCompatGoal = true)]
        public void GenericPartImportsTypeParameter(IContainer container)
        {
            var genericPart = container.GetExportedValue<PartThatImportsT<SomeOtherPart>>();
            Assert.NotNull(genericPart);
            Assert.IsType<SomeOtherPart>(genericPart.Value);
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors, InvalidConfiguration = true)]
        public void GenericPartImportsTypeParameterFailsGracefullyInV3(IContainer container)
        {
            Assert.NotNull(container.GetExportedValue<SomeOtherPart>());
            Assert.Equal(0, container.GetExportedValues<PartThatImportsT<SomeOtherPart>>().Count());
        }

        [Export, Shared, MefV1.Export]
        public class PartThatImportsT<T>
        {
            [Import, MefV1.Import]
            public T Value { get; set; }
        }

        [Export, Shared, MefV1.Export]
        public class SomeOtherPart { }
    }
}
