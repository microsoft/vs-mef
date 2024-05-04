// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using MessagePack;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using MessagePack.Formatters;
    using System.Collections.Immutable;


    //[Union(0, typeof(FieldRef))]
    //[Union(1, typeof(MethodRef))]
    //[Union(2, typeof(PropertyRef))]
    //[MessagePackObject(true)]

    public class MemberRefFormatter<T> : IMessagePackFormatter<T>
        where T : MemberRef
    {
        public enum MemberRefType
        {
            Other = 0,
            Field,
            Property,
            Method,
        }

        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            

            switch (value)
            {
                case FieldRef fieldRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Field, options);

                    SerializefieldRef(ref writer, fieldRef, options);
                    break;
                case PropertyRef propertyRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Property, options);

                    SerializemethodPropertyRef(ref writer, propertyRef, options);

                    break;
                case MethodRef methodRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Method, options);

                    SerializeMethodRef(ref writer, methodRef, options);

                    break;
                default:
                    // this.writer.Write((byte)0)
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Other, options);
                    // ;
                    break;
            }


            void SerializefieldRef(ref MessagePackWriter writer, FieldRef value, MessagePackSerializerOptions options)
            {

                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.FieldTypeRef, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
            }

            void SerializeMethodRef(ref MessagePackWriter writer, MethodRef value, MessagePackSerializerOptions options)
            {



                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
                options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.ParameterTypes, options);
                options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.GenericMethodArguments, options);
            }

            void SerializemethodPropertyRef(ref MessagePackWriter writer, PropertyRef value, MessagePackSerializerOptions options)
            {


                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.PropertyTypeRef, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
                options.Resolver.GetFormatterWithVerify<int?>().Serialize(ref writer, value.SetMethodMetadataToken, options);
                options.Resolver.GetFormatterWithVerify<int?>().Serialize(ref writer, value.GetMethodMetadataToken, options);
            }


        }
    


            //    options.Resolver.GetFormatterWithVerify<AssemblyName>().Serialize(ref writer, value.Name, options);





        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            MemberRefType kind = options.Resolver.GetFormatterWithVerify<MemberRefType>().Deserialize(ref reader, options);

            switch (kind)
            {
                case MemberRefType.Other:
                    return default(MemberRef) as T;
                case MemberRefType.Field:
                    return ReadFieldRef(ref reader, options);
                case MemberRefType.Property:
                    return ReadPropertyRef(ref reader, options);
                case MemberRefType.Method:
                    return ReadMethodRef(ref reader, options);
                default:
                    throw new NotSupportedException();
            }


            T ReadFieldRef(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                var fieldType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                var metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                var name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                var isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

                var value = new FieldRef(declaringType, fieldType, metadataToken, name, isStatic);

                return value as T;
            }

            T ReadPropertyRef(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                var declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                var propertyType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                var metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                var name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                var isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                var setter = options.Resolver.GetFormatterWithVerify<int?>().Deserialize(ref reader, options);
                var getter = options.Resolver.GetFormatterWithVerify<int?>().Deserialize(ref reader, options);
               

                var value = new PropertyRef(
                        declaringType,
                        propertyType,
                        metadataToken,
                        getter,
                        setter,
                        name,
                        isStatic);
                return value as T;
            }

            T ReadMethodRef(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {

                var declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                var metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                var name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                var isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                var parameterTypes = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Deserialize(ref reader, options);
                var genericMethodArguments = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Deserialize(ref reader, options);

                var value = new MethodRef(declaringType, metadataToken, name, isStatic, parameterTypes, genericMethodArguments);

                return value as T;


            }

        }
    }



    [MessagePackFormatter(typeof(MemberRefFormatter<MemberRef>))]

    public abstract class MemberRef : IEquatable<MemberRef>
    {
        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? metadataToken;

        /// <summary>
        /// The <see cref="MemberInfo"/> that this value was instantiated with,
        /// or cached later when a metadata token was resolved.
        /// </summary>
        private MemberInfo? cachedMemberInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberRef"/> class.
        /// </summary>
        protected MemberRef(TypeRef declaringType, int metadataToken, bool isStatic)
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            this.DeclaringType = declaringType;
            this.metadataToken = metadataToken;
            this.IsStatic = isStatic;
        }

        protected MemberRef(TypeRef declaringType, MemberInfo cachedMemberInfo)
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            Requires.NotNull(cachedMemberInfo, nameof(cachedMemberInfo));

            this.DeclaringType = declaringType;
            this.cachedMemberInfo = cachedMemberInfo;
            this.IsStatic = cachedMemberInfo.IsStatic();
        }

        protected MemberRef(MemberInfo memberInfo, Resolver resolver)
            : this(
                 TypeRef.Get(Requires.NotNull(memberInfo, nameof(memberInfo)).DeclaringType ?? throw new ArgumentException("DeclaringType is null", nameof(memberInfo)), resolver),
                 memberInfo)
        {
        }

       // [Key(0)]
        public TypeRef DeclaringType { get; }

      //  [Key(1)]
        public AssemblyName AssemblyName => this.DeclaringType.AssemblyName;

      //  [Key(2)]
        public abstract string Name { get; }

      //  [Key(3)]
        public bool IsStatic { get; }

      //  [Key(4)]
        public int MetadataToken => this.metadataToken ?? this.cachedMemberInfo?.GetMetadataTokenSafe() ?? 0;

        //[Key(5)]
        [IgnoreMember] //// TODO Ankit ignoer it
        public MemberInfo MemberInfo => this.cachedMemberInfo ?? (this.cachedMemberInfo = this.Resolve());

        internal MemberInfo? MemberInfoNoResolve => this.cachedMemberInfo;

        internal Resolver Resolver => this.DeclaringType.Resolver;

        [return: NotNullIfNotNull("member")]
        public static MemberRef? Get(MemberInfo member, Resolver resolver)
        {
            if (member == null)
            {
                return null;
            }

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return new FieldRef((FieldInfo)member, resolver);
                case MemberTypes.Constructor:
                case MemberTypes.Method:
                    return new MethodRef((MethodInfo)member, resolver);
                case MemberTypes.Property:
                    return new PropertyRef((PropertyInfo)member, resolver);
                default:
                    throw new NotSupportedException();
            }
        }

        public virtual bool Equals(MemberRef? other)
        {
            if (other == null || !this.GetType().IsEquivalentTo(other.GetType()))
            {
                return false;
            }

            if (this.cachedMemberInfo != null && other.cachedMemberInfo != null)
            {
                if (this.cachedMemberInfo == other.cachedMemberInfo)
                {
                    return true;
                }
            }

            if (this.metadataToken.HasValue && other.metadataToken.HasValue && this.DeclaringType.AssemblyId.Equals(other.DeclaringType.AssemblyId))
            {
                if (this.metadataToken.Value != other.metadataToken.Value)
                {
                    return false;
                }
            }
            else
            {
                if (!this.EqualsByTypeLocalMetadata(other))
                {
                    return false;
                }
            }

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is equivalent to another one,
        /// based only on metadata that describes this member, assuming the declaring types are equal.
        /// </summary>
        /// <param name="other">The instance to compare with. This may be assumed to always be an instance of the same type.</param>
        /// <returns><see langword="true"/> if the local metadata on the member are equal; <see langword="false"/> otherwise.</returns>
        protected abstract bool EqualsByTypeLocalMetadata(MemberRef other);

        protected abstract MemberInfo Resolve();

        internal abstract void GetInputAssemblies(ISet<AssemblyName> assemblies);

        public override int GetHashCode()
        {
            // Derived types must override this.
            throw new NotImplementedException();
        }

        public override bool Equals(object? obj)
        {
            return obj is MemberRef && this.Equals((MemberRef)obj);
        }
    }
}
