// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.MissingAssemblyTests;

    /// <summary>
    /// This type is here to be a thorn in the side of assembly discovery code.
    /// It has an attribute that comes from an assembly that will be missing at runtime.
    /// </summary>
    [NotFound]
    public class TypeWithMissingAttribute
    {
    }
}
