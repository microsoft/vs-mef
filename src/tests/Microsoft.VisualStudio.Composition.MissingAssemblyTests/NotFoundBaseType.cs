// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.MissingAssemblyTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// This type is defined in an assembly that should *not* be found
    /// during unit testing. It is intentionally defined this way to test
    /// a part discovery's handling of exceptions thrown from Assembly.GetTypes calls.
    /// </summary>
    public class NotFoundBaseType
    {
    }
}
