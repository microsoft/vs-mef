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

    [Export]
#if DESKTOP
    [MefV1.Export]
#endif
    public class DiscoverablePart1
    {
    }
}
