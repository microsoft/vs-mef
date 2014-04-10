namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Obsolete", "BuildBreak")]
    public class ObsoleteAttributeTests
    {
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, typeof(PartWithObsoleteConstructor))]
        public void ObsoleteConstructor(IContainer container)
        {
            var export = container.GetExportedValue<PartWithObsoleteConstructor>();
            Assert.NotNull(export);
        }

        [MefV1.Export, Export]
        public class PartWithObsoleteConstructor
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            public PartWithObsoleteConstructor()
            {
            }
        }

        // TODO: Add tests for accessing properties, methods, etc. with Obsolete attributes.
    }
}
