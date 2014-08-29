namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Reflection;
    using Validation;

    internal class LazyMetadataWrapper : ExportProvider.IMetadataDictionary
    {
        private static readonly HashSet<Assembly> AlwaysLoadedAssemblies = new HashSet<Assembly>(new[] {
            typeof(CreationPolicy).Assembly,
            typeof(string).Assembly,
        });

        /// <summary>
        /// The direction of value translation for this instance.
        /// </summary>
        private readonly Direction direction;

        /// <summary>
        /// The underlying metadata, which may be partially translated since value translation may choose
        /// to persist the translated result.
        /// </summary>
        protected ImmutableDictionary<string, object> underlyingMetadata;

        internal LazyMetadataWrapper(ImmutableDictionary<string, object> metadata, Direction direction)
        {
            Requires.NotNull(metadata, "metadata");

            this.direction = direction;
            this.underlyingMetadata = metadata;
        }

        internal enum Direction
        {
            /// <summary>
            /// The metadata wrapper will replace instances of Type with TypeRef, and other such serialization substitutions.
            /// </summary>
            ToSubstitutedValue,

            /// <summary>
            /// The metadata wrapper will reverse the <see cref="ToSubstitutedValue"/> operation, restoring Type where TypeRef is found, etc.
            /// </summary>
            ToOriginalValue,
        }

        public bool ContainsKey(string key)
        {
            return this.underlyingMetadata.ContainsKey(key);
        }

        public IEnumerable<string> Keys
        {
            get { return this.underlyingMetadata.Keys; }
        }

        public bool TryGetValue(string key, out object value)
        {
            object underlyingValue;
            if (this.underlyingMetadata.TryGetValue(key, out underlyingValue))
            {
                value = this.SubstituteValueIfRequired(key, underlyingValue);
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerable<object> Values
        {
            get
            {
                return from pair in this
                       let value = this.SubstituteValueIfRequired(pair.Key, pair.Value)
                       select value;
            }
        }

        public object this[string key]
        {
            get { return this.SubstituteValueIfRequired(key, this.underlyingMetadata[key]); }
            set { throw new NotSupportedException(); }
        }

        public int Count
        {
            get { return this.underlyingMetadata.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var enumerable = from pair in this.underlyingMetadata
                             select new KeyValuePair<string, object>(pair.Key, this.SubstituteValueIfRequired(pair.Key, pair.Value));
            return enumerable.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Add(string key, object value)
        {
            throw new NotSupportedException();
        }

        ICollection<string> IDictionary<string, object>.Keys
        {
            get
            {
                IDictionary<string, object> metadata = this.underlyingMetadata;
                return metadata.Keys;
            }
        }

        public bool Remove(string key)
        {
            throw new NotSupportedException();
        }

        ICollection<object> IDictionary<string, object>.Values
        {
            get
            {
                return this.Values.ToImmutableArray();
            }
        }

        public void Add(KeyValuePair<string, object> item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            object value;
            if (this.underlyingMetadata.TryGetValue(item.Key, out value))
            {
                value = this.SubstituteValueIfRequired(item.Key, value);
                return item.Value == value;
            }

            return false;
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            foreach (var pair in this)
            {
                array[arrayIndex++] = pair;
            }
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            throw new NotSupportedException();
        }

        internal static IReadOnlyDictionary<string, object> TryUnwrap(IReadOnlyDictionary<string, object> metadata)
        {
            var self = metadata as LazyMetadataWrapper;
            if (self != null)
            {
                return self.underlyingMetadata;
            }

            return metadata;
        }

        internal static IReadOnlyDictionary<string, object> Rewrap(IReadOnlyDictionary<string, object> originalWrapper, IReadOnlyDictionary<string, object> updatedMetadata)
        {
            var self = originalWrapper as LazyMetadataWrapper;
            if (self != null)
            {
                return self.Clone(self, updatedMetadata);
            }

            return updatedMetadata;
        }

        protected virtual LazyMetadataWrapper Clone(LazyMetadataWrapper oldVersion, IReadOnlyDictionary<string, object> newMetadata)
        {
            return new LazyMetadataWrapper(newMetadata.ToImmutableDictionary(), oldVersion.direction);
        }

        protected virtual object SubstituteValueIfRequired(string key, object value)
        {
            Requires.NotNull(key, "key");

            if (value == null)
            {
                return null;
            }

            bool preserveTranslation = false;
            ISubstitutedValue substitutedValue;
            switch (this.direction)
            {
                case Direction.ToSubstitutedValue:
                    Type valueAsType;
                    Type[] valueAsTypeArray;
                    Type typeOfValue;
                    if ((valueAsType = value as Type) != null)
                    {
                        value = TypeRef.Get(valueAsType);
                    }
                    else if ((valueAsTypeArray = value as Type[]) != null)
                    {
                        value = valueAsTypeArray.Select(TypeRef.Get).ToArray();
                        preserveTranslation = true;
                    }
                    else if (IsTypeWorthDeferring(typeOfValue = value.GetType()))
                    {
                        if (Enum32Substitution.TrySubstituteValue(value, out substitutedValue))
                        {
                            value = substitutedValue;
                        }
                    }

                    break;
                case Direction.ToOriginalValue:
                    TypeRef valueAsTypeRef;
                    TypeRef[] valueAsTypeRefArray;
                    if ((valueAsTypeRef = value as TypeRef) != null)
                    {
                        value = Resolver.Resolve(valueAsTypeRef);
                    }
                    else if ((valueAsTypeRefArray = value as TypeRef[]) != null)
                    {
                        value = valueAsTypeRefArray.Select(Resolver.Resolve).ToArray();
                        preserveTranslation = true;
                    }
                    else if ((substitutedValue = value as ISubstitutedValue) != null)
                    {
                        value = substitutedValue.ActualValue;
                    }

                    break;
                default:
                    throw Assumes.NotReachable();
            }

            if (preserveTranslation)
            {
                // Update our metadata dictionary with the substitution to avoid
                // the translation costs next time.
                this.underlyingMetadata = this.underlyingMetadata.SetItem(key, value);
            }

            return value;
        }

        private static bool IsTypeWorthDeferring(Type typeOfValue)
        {
            Requires.NotNull(typeOfValue, "typeOfValue");

            return !AlwaysLoadedAssemblies.Contains(typeOfValue.Assembly);
        }

        internal interface ISubstitutedValue
        {
            object ActualValue { get; }
        }

        internal class Enum32Substitution : ISubstitutedValue, IEquatable<Enum32Substitution>
        {
            internal Enum32Substitution(TypeRef enumType, int rawValue)
            {
                this.EnumType = enumType;
                this.RawValue = rawValue;
            }

            internal static bool TrySubstituteValue(object value, out ISubstitutedValue substitutedValue)
            {
                if (value != null)
                {
                    Type valueType = value.GetType();
                    if (valueType.IsEnum && Enum.GetUnderlyingType(valueType) == typeof(int))
                    {
                        substitutedValue = new Enum32Substitution(TypeRef.Get(valueType), (int)value);
                        return true;
                    }
                }

                substitutedValue = null;
                return false;
            }

            public object ActualValue
            {
                get { return Enum.ToObject(this.EnumType.Resolve(), this.RawValue); }
            }

            internal TypeRef EnumType { get; private set; }

            internal int RawValue { get; private set; }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj is Enum32Substitution)
                {
                    return this.Equals((Enum32Substitution)obj);
                }

                ISubstitutedValue other;
                if (TrySubstituteValue(obj, out other))
                {
                    return this.Equals((Enum32Substitution)other);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return this.EnumType.GetHashCode() ^ this.RawValue;
            }

            public bool Equals(Enum32Substitution other)
            {
                if (other == null)
                {
                    return false;
                }

                return this.EnumType.Equals(other.EnumType)
                    && this.RawValue == other.RawValue;
            }
        }
    }
}
