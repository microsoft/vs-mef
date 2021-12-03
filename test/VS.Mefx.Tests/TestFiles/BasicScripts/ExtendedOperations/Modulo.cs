// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ExtendedOperations
{
    using System.Composition;
    using MefCalculator;

    public class Modulo : IOperation
    {
        public int Operate(int left, int right)
        {
            return left % right;
        }

        [Import]
        public ChainOne? AddInput { get; }
    }
}
