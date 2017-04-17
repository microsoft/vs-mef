// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AppDomainTests2
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Export, CustomMetadata]
    [ExportMetadata("Simple", "Value")]
    public class ExportWithCustomMetadata
    {
    }
}
