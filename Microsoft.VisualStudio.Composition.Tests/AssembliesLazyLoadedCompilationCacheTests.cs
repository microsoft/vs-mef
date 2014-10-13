namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    [Trait("CodeGen", "true")]
    public class AssembliesLazyLoadedCompilationCacheTests : AssembliesLazyLoadedTests
    {
        public AssembliesLazyLoadedCompilationCacheTests()
            : base(new CompiledComposition { AssemblyName = "AssembliesLazyLoadedTestsCompilation" })
        {
        }
    }
}
