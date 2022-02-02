// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ExtendedOperations
{
    using System.ComponentModel.Composition;
    using MefCalculator;

    [Export]
    public class ChainOne
    {
        [Import]
        public AddIn? Adder { get; set; }
    }
}
