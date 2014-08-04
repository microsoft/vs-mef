namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class ByValueEquality
    {
        internal static IEqualityComparer<IReadOnlyDictionary<string, object>> Metadata
        {
            get { return MetadataDictionaryEqualityComparer.Default; }
        }

        internal static IEqualityComparer<IReadOnlyDictionary<TKey, TValue>> Dictionary<TKey, TValue>()
        {
            return DictionaryEqualityComparer<TKey, TValue>.Default;
        }

        internal static IEqualityComparer<IReadOnlyDictionary<TKey, ImmutableHashSet<TValue>>> DictionaryOfImmutableHashSet<TKey, TValue>()
        {
            return DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue>.Default;
        }

        private class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<IReadOnlyDictionary<TKey, TValue>>
        {
            internal static readonly DictionaryEqualityComparer<TKey, TValue> Default = new DictionaryEqualityComparer<TKey, TValue>();

            protected virtual IEqualityComparer<TValue> ValueComparer
            {
                get { return EqualityComparer<TValue>.Default; }
            }

            public bool Equals(IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y)
            {
                if (x.Count != y.Count)
                {
                    return false;
                }

                IEqualityComparer<TValue> valueComparer = this.ValueComparer;
                foreach (var pair in x)
                {
                    TValue otherValue;
                    if (!y.TryGetValue(pair.Key, out otherValue))
                    {
                        return false;
                    }

                    if (!valueComparer.Equals(pair.Value, otherValue))
                    {
                        return false;
                    }
                }

                return true;
            }

            public virtual int GetHashCode(IReadOnlyDictionary<TKey, TValue> obj)
            {
                int hash = obj.Count;
                foreach (var pair in obj)
                {
                    hash += pair.Key.GetHashCode();
                }

                return obj.Count;
            }
        }

        private class MetadataDictionaryEqualityComparer : DictionaryEqualityComparer<string, object>
        {
            new internal static readonly MetadataDictionaryEqualityComparer Default = new MetadataDictionaryEqualityComparer();

            public override int GetHashCode(IReadOnlyDictionary<string, object> obj)
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

        private class DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue> : DictionaryEqualityComparer<TKey, ImmutableHashSet<TValue>>
        {
            new internal static readonly DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue> Default = new DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue>();
            
            protected override IEqualityComparer<ImmutableHashSet<TValue>> ValueComparer
            {
                get { return SetEqualityComparer.Default; }
            }

            private class SetEqualityComparer : IEqualityComparer<ImmutableHashSet<TValue>>
            {
                internal static readonly SetEqualityComparer Default = new SetEqualityComparer();

                private SetEqualityComparer() { }

                public bool Equals(ImmutableHashSet<TValue> x, ImmutableHashSet<TValue> y)
                {
                    if (x == null ^ y == null)
                    {
                        return false;
                    }

                    if (x == null)
                    {
                        return true;
                    }

                    return x.SetEquals(y);
                }

                public int GetHashCode(ImmutableHashSet<TValue> obj)
                {
                    return obj.Count;
                }
            }
        }
    }
}
