// Copyright (c) Microsoft. All rights reserved.

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
        public MemberTypeToExport ImportingProperty { get; set; }
    }
}
