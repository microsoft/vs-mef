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
