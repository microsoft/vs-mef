// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.ComponentModel.Composition;

    public class ExportMeta
    {
        [Export("MetdadataTest")]
        [ExportMetadata("Value", 5)]
        public int ExportOne { get; set; } = 5;

        [Export("MetdadataTest")]
        [ExportMetadata("Value", 7)]
        public string ExportTwo { get; set; } = "7";
    }
}
