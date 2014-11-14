namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    // TODO: remove "abstract" from the class definition to re-enable these tests when codegen is fixed.
    [Trait("CodeGen", "true")]
    public abstract class CompiledCacheAndReloadTests : CacheAndReloadTests
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
