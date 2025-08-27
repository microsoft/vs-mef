// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppDomainTests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;

    [Export]
    [ExportMetadata("YetAnotherExport", typeof(YetAnotherExport))]
    public class ExternalExportWithExternalMetadataType
    {
    }

    [Export]
    [ExportMetadata("YetAnotherExports", typeof(YetAnotherExport))]
    [ExportMetadata("YetAnotherExports", typeof(YetAnotherExport))]
    public class ExternalExportWithExternalMetadataTypeArray
    {
    }

    [Export]
    [ExportMetadata("CustomEnum", CustomEnum.Value1)]
    public class ExternalExportWithExternalMetadataEnum32
    {
    }

    [Export]
    [ExportMetadata("CustomEnumArray", CustomEnum.Value1)]
    [ExportMetadata("CustomEnumArray", CustomEnum.Value2)]
    public class ExternalExportWithExternalMetadataEnumArray
    {
    }

    [Export]
    [ExportMetadata("CustomEnumArray", CustomEnum.Value1)]
    public class ExternalExportWithExternalMetadataEnumSingleInArray
    {
    }
}
