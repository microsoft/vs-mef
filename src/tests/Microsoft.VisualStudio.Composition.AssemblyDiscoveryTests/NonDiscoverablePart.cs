// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
#if DESKTOP
    using MefV1 = System.ComponentModel.Composition;
#endif

    [PartNotDiscoverable]
    [Export]
#if DESKTOP
    [MefV1.PartNotDiscoverable]
    [MefV1.Export]
#endif
    public class NonDiscoverablePart
    {
    }
}
