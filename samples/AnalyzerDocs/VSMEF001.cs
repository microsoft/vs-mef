// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.Composition;

namespace Samples.AnalyzerDocs.VSMEF001
{
#pragma warning disable VSMEF001
    namespace Defective
    {
        public class Class1
        {
            #region Defective
            [Import]
            object SomeProperty { get; } // VSMEF001 reported here
            #endregion
        }
    }
#pragma warning restore VSMEF001

    namespace Fixed
    {
        public class Class1
        {
            #region Fix
            [Import]
            object SomeProperty { get; set; }
            #endregion
        }
    }
}
