// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
