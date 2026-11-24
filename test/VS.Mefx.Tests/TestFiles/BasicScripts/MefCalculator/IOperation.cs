// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MefCalculator
{
    using System.ComponentModel.Composition;

    public interface IOperation
    {
        public int Operate(int left, int right);
    }

    [Export(typeof(IOperation))]
    [ExportMetadata("Symbol", "+")]
#pragma warning disable SA1649 // The fixture groups operations with their interface.
    public class Add : IOperation
#pragma warning restore SA1649
    {
        public int Operate(int left, int right)
        {
            return left + right;
        }
    }

    [Export(typeof(IOperation))]
    [ExportMetadata("Symbol", "-")]
    public class Subtract : IOperation
    {
        public int Operate(int left, int right)
        {
            return left - right;
        }
    }
}
