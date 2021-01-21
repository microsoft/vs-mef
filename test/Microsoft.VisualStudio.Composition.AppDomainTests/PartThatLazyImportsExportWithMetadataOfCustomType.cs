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
    public class PartThatLazyImportsExportWithMetadataOfCustomType
    {
        [Import("Microsoft.VisualStudio.Composition.AppDomainTests2.ExportWithCustomMetadata")]
        public Lazy<object, IDictionary<string, object?>> ImportingProperty { get; set; } = null!;
    }
}
