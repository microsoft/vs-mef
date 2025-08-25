// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppDomainTests2
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Export]
    [CustomEnumArrayMetadata(CustomEnum.Value1)]
    [CustomEnumArrayMetadata(CustomEnum.Value2)]
    public class ExportWithCustomEnumArrayMetadata
    {
    }

    [Export]
    [CustomEnumArrayMetadata(CustomEnum.Value1)]
    public class ExportWithCustomEnumSingleInArrayMetadata
    {
    }
}