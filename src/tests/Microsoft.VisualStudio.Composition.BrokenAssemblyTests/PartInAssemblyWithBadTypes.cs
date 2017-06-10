// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.BrokenAssemblyTests
{
    using System.Composition;
#if DESKTOP
    using MefV1 = System.ComponentModel.Composition;
#endif

    [Export]
#if DESKTOP
    [MefV1.Export]
#endif
    public class GoodPartInAssemblyWithBadTypes
    {
    }
}
