// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Text;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;

    [Export]
    public class PartThatImportsExportedMember
    {
        [Import]
        public MemberTypeToExport ImportingProperty { get; set; } = null!;
    }
}
