// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System.Composition;

    public class ExternalExportOnMember
    {
        [Export]
        [ExportMetadata("MetadataWithString", "AnotherString")]
        public ExternalExportOnMember ExportingProperty => new ExternalExportOnMember();
    }
}
