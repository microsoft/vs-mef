namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class NestedTypeTests
    {
        [MefFact(CompositionEngines.V1Compat)]
        public void NestedTypeOfGenericType(IContainer container)
        {
            var export = container.GetExportedValue<OuterDerivedPart>();
            Assert.NotNull(export);
        }

        public abstract class OuterBasePart<T>
        {
            [MefV1.Import]
            public NestedType Value { get; set; }

            [MefV1.Export]
            public class NestedType { }

            [MefV1.Export]
            public class NestedType2 { }
        }

        [MefV1.Export]
        public class OuterDerivedPart : OuterBasePart<int> { }
    }
}
