// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class ThrowingPartsTests
    {
        // Test backlog:
        // DIMENSION 1
        //  * part throws in ctor
        //  * part throws in exporting property
        // DIMENSION 2
        //  * throwing part is imported directly by another part.
        //  * throwing part is imported lazily by another part.
        //  * throwing part is retrieved via ExportProvider.GetExportedValue<T>
        //  * throwing part is retrieved via ExportProvider.GetExport<T> (Lazy return value)
        // DESIGN: throw an exception type consistent with the library that owns the [Import] site
        //         or the ExportProvider that threw the exception.
    }
}
