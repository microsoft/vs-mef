// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.Composition;

    public class ExportTest
    {
        [Export("MajorRevision")]
        public int MajorRevision { get; } = 4;

        [Export("MinorRevision")]
        public int MinorRevision { get; } = 0;

        [Import("ChainOneString")]
        public int? TypeMismatch { get; }
    }
}
