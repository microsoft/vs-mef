// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ExtendedOperations
{
    using System.Composition;

    [Export]
    public class ChainOne
    {
        public string ChainName { get; } = "First Chain";

        [Import]
        public MefCalculator.AddIn? Adder { get; }
    }
}
