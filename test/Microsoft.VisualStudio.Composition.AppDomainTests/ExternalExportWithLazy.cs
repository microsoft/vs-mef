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

    [Export]
    public class ExternalExportWithLazy
    {
        [Import("YetAnotherExport")]
        public Lazy<object> YetAnotherExport { get; set; } = null!;
    }
}
