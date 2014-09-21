namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class CompiledCacheAndReloadTests : CacheAndReloadTests
    {
        public CompiledCacheAndReloadTests()
            : base(new CompiledComposition
            {
                AssemblyName = "CacheAndReloadTestCompilation",
            })
        {
        }
    }
}
