namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

#if Runtime
    public class AssembliesLazyLoadedDataFileCacheTests : AssembliesLazyLoadedTests
    {
        public AssembliesLazyLoadedDataFileCacheTests()
            : base(new CachedComposition())
        {
        }
    }
#endif
}
