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
        public void GenericPartImportsTypeArgument(IContainer container)
        {
            var genericPart = container.GetExportedValue<PartThatImportsT<SomeOtherPart>>();
            Assert.NotNull(genericPart);
            Assert.IsType<SomeOtherPart>(genericPart.Value);
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
