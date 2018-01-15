#if NET45

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    /// <summary>
    /// This class very particularly is only defined in NET45
    /// and omitted from other frameworks.
    /// It has a name that starts with "A" so that the compiler will
    /// tend to store its ctor metadata token early in the table,
    /// allowing us to test for metadata token changes between versions
    /// of the assembly.
    /// These conditions are verified by the test that requires them.
    /// </summary>
    internal class AConditionalClass
    {
    }
}

#endif
