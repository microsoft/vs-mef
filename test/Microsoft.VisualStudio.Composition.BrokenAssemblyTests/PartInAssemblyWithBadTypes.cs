// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1649 // File name should match first type name

namespace Microsoft.VisualStudio.Composition.BrokenAssemblyTests
{
    using System.Composition;
    using MefV1 = System.ComponentModel.Composition;

    [Export, MefV1.Export]
    public class GoodPartInAssemblyWithBadTypes
    {
    }
}
