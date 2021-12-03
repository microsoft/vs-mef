// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.Composition;

    [Export]
    public class AddIn
    {
        [Import("ChainOne")]
        public string? FieldOne { get; set; }
    }
}
