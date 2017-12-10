// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal static partial class ByValueEquality
    {
        internal static IEqualityComparer<IReadOnlyDictionary<string, object>> Metadata
        {
            get { return MetadataDictionaryEqualityComparer.Default; }
        }

        internal static IEqualityComparer<IReadOnlyDictionary<TKey, TValue>> Dictionary<TKey, TValue>(IEqualityComparer<TValue> valueComparer = null)
        {
            return DictionaryEqualityComparer<TKey, TValue>.Get(valueComparer);
        }

        internal static IEqualityComparer<IReadOnlyDictionary<TKey, ImmutableHashSet<TValue>>> DictionaryOfImmutableHashSet<TKey, TValue>()
        {
            return DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue>.Default;
        }

        internal static IEqualityComparer<IReadOnlyCollection<T>> EquivalentIgnoreOrder<T>()
        {
            return CollectionIgnoreOrder<T>.Default;
        }

        private class CollectionIgnoreOrder<T> : IEqualityComparer<IReadOnlyCollection<T>>
        {
            internal static readonly CollectionIgnoreOrder<T> Default = new CollectionIgnoreOrder<T>();

            private CollectionIgnoreOrder()
            {
            }

            protected virtual IEqualityComparer<T> ValueComparer
            {
                get { return EqualityComparer<T>.Default; }
            }

            public bool Equals(IReadOnlyCollection<T> x, IReadOnlyCollection<T> y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null ^ y == null)
                {
                    return false;
                }

                if (x == null)
                {
                    return true;
                }

                if (x.Count != y.Count)
                {
                    return false;
                }

                IEqualityComparer<T> valueComparer = this.ValueComparer;
                var matchingIndexesInY = new bool[y.Count];
                var yList = y as IReadOnlyList<T> ?? y.ToList();
                foreach (T item in x)
                {
                    int j;
                    for (j = 0; j < y.Count; j++)
                    {
                        if (!matchingIndexesInY[j] && valueComparer.Equals(item, yList[j]))
                        {
                            // We found a match. Avoid finding the same item again.
                            matchingIndexesInY[j] = true;
                            break;
                        }
                    }

                    if (j == y.Count)
                    {
                        // no match
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(IReadOnlyCollection<T> obj)
            {
                int hashCode = obj.Count;
                foreach (var item in obj)
                {
                    hashCode += item.GetHashCode();
                }

                return hashCode;
            }
        }

        private class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<IReadOnlyDictionary<TKey, TValue>>
        {
            private readonly IEqualityComparer<TValue> valueComparer;

            internal static readonly DictionaryEqualityComparer<TKey, TValue> Default = new DictionaryEqualityComparer<TKey, TValue>();

            protected DictionaryEqualityComparer(IEqualityComparer<TValue> valueComparer = null)
            {
                this.valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
            }

            internal static DictionaryEqualityComparer<TKey, TValue> Get(IEqualityComparer<TValue> valueComparer = null)
            {
                if (valueComparer == null || valueComparer == EqualityComparer<TValue>.Default)
                {
                    return Default;
                }
                else
                {
                    return new DictionaryEqualityComparer<TKey, TValue>(valueComparer);
                }
            }

            public virtual bool Equals(IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x.Count != y.Count)
                {
                    return false;
                }

                foreach (var pair in x)
                {
                    TValue otherValue;
                    if (!y.TryGetValue(pair.Key, out otherValue))
                    {
                        return false;
                    }

                    if (!this.valueComparer.Equals(pair.Value, otherValue))
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
            internal static new readonly MetadataDictionaryEqualityComparer Default = new MetadataDictionaryEqualityComparer();

            protected MetadataDictionaryEqualityComparer()
                : base(MetadataValueComparer.Default)
            {
            }

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

            public override bool Equals(IReadOnlyDictionary<string, object> x, IReadOnlyDictionary<string, object> y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                // Be sure we're comparing TypeRefs instead of resolved Types to avoid loading assemblies unnecessarily.
                return base.Equals(LazyMetadataWrapper.TryUnwrap(x), LazyMetadataWrapper.TryUnwrap(y));
            }

            private class MetadataValueComparer : IEqualityComparer<object>
            {
                internal static readonly MetadataValueComparer Default = new MetadataValueComparer();

                private MetadataValueComparer()
                {
                }

                public new bool Equals(object x, object y)
                {
                    if (x == y)
                    {
                        return true;
                    }

                    if (x == null ^ y == null)
                    {
                        return false;
                    }

                    var xSubstituted = x as LazyMetadataWrapper.ISubstitutedValue;
                    var ySubstituted = y as LazyMetadataWrapper.ISubstitutedValue;
                    if (xSubstituted != null || ySubstituted != null)
                    {
                        return xSubstituted != null
                            ? xSubstituted.Equals(y)
                            : ySubstituted.Equals(x);
                    }

                    if (x.GetType() != y.GetType())
                    {
                        // Whitelist Type[] and RuntimeType[] arrays as equivalent.
                        if (!(x.GetType().IsArray && y.GetType().IsArray &&
                            typeof(Type).GetTypeInfo().IsAssignableFrom(x.GetType().GetElementType()) &&
                            typeof(Type).GetTypeInfo().IsAssignableFrom(y.GetType().GetElementType())))
                        {
                            return false;
                        }
                    }

                    if (x.GetType().IsArray)
                    {
                        return ArrayEquals((Array)x, (Array)y, v => v);
                    }
                    else
                    {
                        return x.Equals(y);
                    }
                }

                private static bool ArrayEquals(Array xArray, Array yArray, Func<object, object> translator)
                {
                    if (xArray.Length != yArray.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < xArray.Length; i++)
                    {
                        if (!EqualityComparer<object>.Default.Equals(translator(xArray.GetValue(i)), translator(yArray.GetValue(i))))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                public int GetHashCode(object obj)
                {
                    throw new NotImplementedException();
                }
            }
        }

        private class DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue> : DictionaryEqualityComparer<TKey, ImmutableHashSet<TValue>>
        {
            internal static new readonly DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue> Default = new DictionaryOfImmutableHashSetEqualityComparer<TKey, TValue>();

            protected DictionaryOfImmutableHashSetEqualityComparer()
                : base(SetEqualityComparer.Default)
            {
            }

            private class SetEqualityComparer : IEqualityComparer<ImmutableHashSet<TValue>>
            {
                internal static readonly SetEqualityComparer Default = new SetEqualityComparer();

                private SetEqualityComparer()
                {
                }

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
