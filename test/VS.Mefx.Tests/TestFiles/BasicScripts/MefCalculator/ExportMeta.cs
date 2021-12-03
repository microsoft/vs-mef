// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.Composition;

    [Export]
    public class ExportMeta
    {
        [Export("MetadataTest")]
        [ExportMetadata("Value", 5)]
        public int ExportOne { get; } = 5;

        [Export("MetadataTest")]
        [ExportMetadata("Value", 7)]
        public string ExportTwo { get; } = "7";
    }
}
