// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;

    public delegate void ReportFaultCallback(Exception e, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export);

    public interface IFaultReportingExportProviderFactory : IExportProviderFactory
    {
        ExportProvider CreateExportProvider(ReportFaultCallback faultCallback);
    }
}
