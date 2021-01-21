// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK // See comments below

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    /// <summary>
    /// This class very particularly is only defined in NETFRAMEWORK
    /// and omitted from NETSTANDARD.
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
