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
}
