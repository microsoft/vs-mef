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
    using Reflection;

    internal class LazyMetadataWrapper : ExportProvider.IMetadataDictionary
    {
        private static readonly HashSet<Assembly> AlwaysLoadedAssemblies = new HashSet<Assembly>(new[] {
            typeof(CreationPolicy).GetTypeInfo().Assembly,
            typeof(string).GetTypeInfo().Assembly,
        });

        /// <summary>
        /// The direction of value translation for this instance.
        /// </summary>
        private readonly Direction direction;

        private readonly Resolver resolver;

        /// <summary>
        /// The underlying metadata, which may be partially translated since value translation may choose
        /// to persist the translated result.
        /// </summary>
        protected ImmutableDictionary<string, object> underlyingMetadata;

        internal LazyMetadataWrapper(ImmutableDictionary<string, object> metadata, Direction direction, Resolver resolver)
        {
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(resolver, nameof(resolver));

            this.direction = direction;
            this.underlyingMetadata = metadata;
            this.resolver = resolver;
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

        internal interface ISubstitutedValue
        {
            object ActualValue { get; }
        }

        public IEnumerable<string> Keys
        {
            get { return this.underlyingMetadata.Keys; }
        }

        ICollection<string> IDictionary<string, object>.Keys
        {
            get
            {
                IDictionary<string, object> metadata = this.underlyingMetadata;
                return metadata.Keys;
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

        ICollection<object> IDictionary<string, object>.Values
        {
            get
            {
                return this.Values.ToImmutableArray();
            }
        }

        public int Count
        {
            get { return this.underlyingMetadata.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public object this[string key]
        {
            get { return this.SubstituteValueIfRequired(key, this.underlyingMetadata[key]); }
            set { throw new NotSupportedException(); }
        }

        public bool ContainsKey(string key)
        {
            return this.underlyingMetadata.ContainsKey(key);
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

        public bool Remove(string key)
        {
            throw new NotSupportedException();
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
            return new LazyMetadataWrapper(newMetadata.ToImmutableDictionary(), oldVersion.direction, this.resolver);
        }

        protected object SubstituteValueIfRequired(string key, object value)
        {
            Requires.NotNull(key, nameof(key));

            if (value == null)
            {
                return null;
            }

            value = this.SubstituteValueIfRequired(value);

            // Update our metadata dictionary with the substitution to avoid
            // the translation costs next time.
            this.underlyingMetadata = this.underlyingMetadata.SetItem(key, value);

            return value;
        }

        protected virtual object SubstituteValueIfRequired(object value)
        {
            Requires.NotNull(value, nameof(value));

            ISubstitutedValue substitutedValue;
            switch (this.direction)
            {
                case Direction.ToSubstitutedValue:
                    if (Enum32Substitution.TrySubstituteValue(value, this.resolver, out substitutedValue) ||
                        TypeSubstitution.TrySubstituteValue(value, this.resolver, out substitutedValue) ||
                        TypeArraySubstitution.TrySubstituteValue(value, this.resolver, out substitutedValue))
                    {
                        value = substitutedValue;
                    }

                    break;
                case Direction.ToOriginalValue:
                    if ((substitutedValue = value as ISubstitutedValue) != null)
                    {
                        value = substitutedValue.ActualValue;
                    }

                    break;
                default:
                    throw Assumes.NotReachable();
            }

            return value;
        }

        private static bool IsTypeWorthDeferring(Type typeOfValue)
        {
            Requires.NotNull(typeOfValue, nameof(typeOfValue));

            return !AlwaysLoadedAssemblies.Contains(typeOfValue.GetTypeInfo().Assembly);
        }

        internal class Enum32Substitution : ISubstitutedValue, IEquatable<Enum32Substitution>
        {
            internal Enum32Substitution(TypeRef enumType, int rawValue)
            {
                Requires.NotNull(enumType, nameof(enumType));

                this.EnumType = enumType;
                this.RawValue = rawValue;
            }

            public object ActualValue
            {
                get { return Enum.ToObject(this.EnumType.Resolve(), this.RawValue); }
            }

            internal TypeRef EnumType { get; private set; }

            internal int RawValue { get; private set; }

            internal static bool TrySubstituteValue(object value, Resolver resolver, out ISubstitutedValue substitutedValue)
            {
                Requires.NotNull(resolver, nameof(resolver));

                if (value != null)
                {
                    Type valueType = value.GetType();
                    if (valueType.GetTypeInfo().IsEnum && Enum.GetUnderlyingType(valueType) == typeof(int) && IsTypeWorthDeferring(valueType))
                    {
                        substitutedValue = new Enum32Substitution(TypeRef.Get(valueType, resolver), (int)value);
                        return true;
                    }
                }

                substitutedValue = null;
                return false;
            }

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
                if (TrySubstituteValue(obj, this.EnumType.Resolver, out other))
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

        internal class TypeSubstitution : ISubstitutedValue, IEquatable<TypeSubstitution>
        {
            internal TypeSubstitution(TypeRef typeRef)
            {
                Requires.NotNull(typeRef, nameof(typeRef));

                this.TypeRef = typeRef;
            }

            internal TypeRef TypeRef { get; private set; }

            public object ActualValue
            {
                get { return this.TypeRef.Resolve(); }
            }

            internal static bool TrySubstituteValue(object value, Resolver resolver, out ISubstitutedValue substitutedValue)
            {
                if (value is Type)
                {
                    substitutedValue = new TypeSubstitution(TypeRef.Get((Type)value, resolver));
                    return true;
                }

                substitutedValue = null;
                return false;
            }

            public override int GetHashCode()
            {
                return this.TypeRef.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj is TypeSubstitution)
                {
                    return this.Equals((TypeSubstitution)obj);
                }

                ISubstitutedValue other;
                if (TrySubstituteValue(obj, this.TypeRef.Resolver, out other))
                {
                    return this.Equals((TypeSubstitution)other);
                }

                return false;
            }

            public bool Equals(TypeSubstitution other)
            {
                if (other == null)
                {
                    return false;
                }

                return this.TypeRef.Equals(other.TypeRef);
            }
        }

        internal class TypeArraySubstitution : ISubstitutedValue, IEquatable<TypeArraySubstitution>
        {
            private readonly Resolver resolver;

            internal TypeArraySubstitution(IReadOnlyList<TypeRef> typeRefArray, Resolver resolver)
            {
                Requires.NotNull(typeRefArray, nameof(typeRefArray));
                Requires.NotNull(resolver, nameof(resolver));

                this.TypeRefArray = typeRefArray;
                this.resolver = resolver;
            }

            internal IReadOnlyList<TypeRef> TypeRefArray { get; private set; }

            public object ActualValue
            {
                get { return this.TypeRefArray.Select(ResolverExtensions.Resolve).ToArray(); }
            }

            internal static bool TrySubstituteValue(object value, Resolver resolver, out ISubstitutedValue substitutedValue)
            {
                if (value is Type[])
                {
                    substitutedValue = new TypeArraySubstitution(((Type[])value).Select(t => TypeRef.Get(t, resolver)).ToImmutableArray(), resolver);
                    return true;
                }

                substitutedValue = null;
                return false;
            }

            public override int GetHashCode()
            {
                return this.TypeRefArray.Count > 0 ? this.TypeRefArray[0].GetHashCode() : 0;
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj is TypeArraySubstitution)
                {
                    return this.Equals((TypeArraySubstitution)obj);
                }

                ISubstitutedValue other;
                if (TrySubstituteValue(obj, this.resolver, out other))
                {
                    return this.Equals((TypeArraySubstitution)other);
                }

                return false;
            }

            public bool Equals(TypeArraySubstitution other)
            {
                if (other == null)
                {
                    return false;
                }

                return this.TypeRefArray.SequenceEqual(other.TypeRefArray);
            }
        }
    }
}
