// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AppDomainTests2
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Text;

    public class ExportedMember
    {
        [Export]
        [ExportMetadata("MetadataWithString", "AnotherString")]
        public MemberTypeToExport ExportingProperty => new MemberTypeToExport();
    }

    public class MemberTypeToExport { }
}
