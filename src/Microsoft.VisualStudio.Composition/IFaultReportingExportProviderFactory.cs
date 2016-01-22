namespace Microsoft.VisualStudio.Composition
{
    using System;

    public delegate void ReportFaultCallback(Exception e, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export);

    public interface IFaultReportingExportProviderFactory : IExportProviderFactory
    {
        ExportProvider CreateExportProvider(ReportFaultCallback faultCallback);
    }
}
