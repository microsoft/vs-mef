// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    /// <summary>
    /// An internal interface that is used as the base interface
    /// of a metadata view defined in another assembly.
    /// </summary>
    internal interface IInternalMetadataView
    {
        string MetadataOnInternalInterface { get; }
    }
}
