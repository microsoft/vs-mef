// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Microsoft.VisualStudio.Threading;

    internal partial class RuntimeExportProviderFactory : IFaultReportingExportProviderFactory
    {
        private readonly RuntimeComposition composition;
        private readonly JoinableTaskFactory? joinableTaskFactory;

        internal RuntimeExportProviderFactory(RuntimeComposition composition, JoinableTaskFactory? joinableTaskFactory = null)
        {
            Requires.NotNull(composition, nameof(composition));
            this.composition = composition;
            this.joinableTaskFactory = joinableTaskFactory;
        }

        public ExportProvider CreateExportProvider()
        {
            return new RuntimeExportProvider(this.composition, this.joinableTaskFactory);
        }

        public ExportProvider CreateExportProvider(ReportFaultCallback faultCallback)
        {
            Requires.NotNull(faultCallback, nameof(faultCallback));
            return new RuntimeExportProvider(this.composition, faultCallback, this.joinableTaskFactory);
        }
    }
}
