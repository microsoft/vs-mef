// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MefV1 = System.ComponentModel.Composition;

    [PartNotDiscoverable]
    [Export]
    [MefV1.PartNotDiscoverable]
    [MefV1.Export]
    public class NonDiscoverablePart
    {
    }
}
