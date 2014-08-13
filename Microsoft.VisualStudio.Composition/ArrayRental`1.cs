namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Validation;

    internal static class ArrayRental<T>
    {
        private static readonly ThreadLocal<Dictionary<int, Stack<T[]>>> arrays = new ThreadLocal<Dictionary<int, Stack<T[]>>>(() => new Dictionary<int, Stack<T[]>>());

        internal static Rental<T[]> Get(int length)
        {
            Stack<T[]> stack;
            if (!arrays.Value.TryGetValue(length, out stack))
            {
                arrays.Value.Add(length, stack = new Stack<T[]>());
            }

            return new Rental<T[]>(stack, len => new T[len], array => Array.Clear(array, 0, array.Length), length);
        }
    }
}
