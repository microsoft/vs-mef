// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests2;

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
