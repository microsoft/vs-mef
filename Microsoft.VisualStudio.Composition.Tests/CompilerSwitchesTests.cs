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

    public class CompilerSwitchesTests
    {
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2)]
        public void CanActivatePartWithObsoleteConstructor(IContainer container)
        {
            var export = container.GetExportedValue<SomeClass>();
            Assert.NotNull(export);
        }

        [MefV1.Export, Export]
        public class SomeClass
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            public SomeClass()
            {
            }
        }
    }
}
