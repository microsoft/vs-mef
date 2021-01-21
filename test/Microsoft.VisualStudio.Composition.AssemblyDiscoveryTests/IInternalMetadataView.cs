// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
