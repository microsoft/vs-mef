using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests2;

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    /// <summary>
    /// An interface deliberately in an assembly that should be
    /// referenced when compiling generated code for the tests that
    /// use this interface.
    /// </summary>
    public interface ISomeInterface
    {
    }

    public interface ISomeInterfaceWithBaseInterface : IBlankInterface
    {
    }
}
