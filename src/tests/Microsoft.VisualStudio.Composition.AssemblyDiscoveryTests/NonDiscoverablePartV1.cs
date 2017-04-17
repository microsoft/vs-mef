namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MefV1 = System.ComponentModel.Composition;

    [MefV1.PartNotDiscoverable]
    [MefV1.Export]
    public class NonDiscoverablePartV1
    {
    }
}
