// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ArrayRental<T>
    {
        private static readonly ThreadLocal<Dictionary<int, Stack<T[]>>> Arrays = new ThreadLocal<Dictionary<int, Stack<T[]>>>(() => new Dictionary<int, Stack<T[]>>());

        internal static Rental<T[]> Get(int length)
        {
            Stack<T[]>? stack;
            if (!Arrays.Value!.TryGetValue(length, out stack))
            {
                Arrays.Value.Add(length, stack = new Stack<T[]>());
            }

            return new Rental<T[]>(stack, len => new T[len], array => Array.Clear(array, 0, array.Length), length);
        }
    }
}
