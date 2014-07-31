namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class ByValueEquality
    {
        internal static IEqualityComparer<IReadOnlyDictionary<string, object>> Metadata
        {
            get { return DictionaryEqualityComparer.Default; }
        }

        private class DictionaryEqualityComparer : IEqualityComparer<IReadOnlyDictionary<string, object>>
        {
            internal static readonly DictionaryEqualityComparer Default = new DictionaryEqualityComparer();

            public bool Equals(IReadOnlyDictionary<string, object> x, IReadOnlyDictionary<string, object> y)
            {
                if (x.Count != y.Count)
                {
                    return false;
                }

                foreach (var pair in x)
                {
                    object otherValue;
                    if (!y.TryGetValue(pair.Key, out otherValue))
                    {
                        return false;
                    }

                    if (pair.Value == null && otherValue == null)
                    {
                        continue;
                    }

                    if (pair.Value == null ^ otherValue == null)
                    {
                        return false;
                    }

                    if (!pair.Value.Equals(otherValue))
                    {
                        return false;
                    }

                    return false;
                }

                return true;
            }

            public int GetHashCode(IReadOnlyDictionary<string, object> obj)
            {
                // We don't want to hash the entire contents. So just look for one key that tends to be on most
                // metadata and usually has a fairly distinguishing value and hash on that.
                string typeIdentity;
                if (obj.TryGetValue(CompositionConstants.ExportTypeIdentityMetadataName, out typeIdentity) && typeIdentity != null)
                {
                    return typeIdentity.GetHashCode();
                }

                // We can't do any better without hashing the entire dictionary.
                return 1;
            }
        }
    }
}
