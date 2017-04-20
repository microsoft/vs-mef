// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.BrokenAssemblyTests
{
    using System.Composition;
    using MefV1 = System.ComponentModel.Composition;

    [Export, MefV1.Export]
    public class GoodPartInAssemblyWithBadTypes
    {
    }
}
