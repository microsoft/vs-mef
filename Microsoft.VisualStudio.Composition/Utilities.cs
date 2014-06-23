namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class Utilities
    {
        internal static bool EqualsByValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> expected, IReadOnlyDictionary<TKey, TValue> actual, IEqualityComparer<TValue> valueComparer = null)
        {
            Requires.NotNull(expected, "expected");
            Requires.NotNull(actual, "actual");
            valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;

            if (expected.Count != actual.Count)
            {
                return false;
            }

            foreach (var entry in expected)
            {
                TValue actualValue;
                if (!actual.TryGetValue(entry.Key, out actualValue))
                {
                    // missing key
                    return false;
                }

                if (!valueComparer.Equals(entry.Value, actualValue))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
