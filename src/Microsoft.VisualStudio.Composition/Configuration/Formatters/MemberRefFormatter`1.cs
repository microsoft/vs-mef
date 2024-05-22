// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Reflection.PortableExecutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition;
    using Microsoft.VisualStudio.Composition.Reflection;

#pragma warning disable CS3001 // Argument type is not CLS-compliant
#pragma warning disable RS0041 // No oblivious reference types

    /// <summary>
    /// A formatter for serializing and deserializing <see cref="MemberRef"/> and its derived types using MessagePack.
    /// </summary>
    /// <typeparam name="TMemberReferenceType">The type of the member reference to serialize and deserialize.</typeparam>
    internal class MemberRefFormatter<TMemberReferenceType> : IMessagePackFormatter<TMemberReferenceType?>
        where TMemberReferenceType : MemberRef
    {
        public static readonly MemberRefFormatter<TMemberReferenceType> Instance = new();

        private MemberRefFormatter()
        {
        }

        /// <inheritdoc/>
        public TMemberReferenceType? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            MemberRefType kind = options.Resolver.GetFormatterWithVerify<MemberRefType>().Deserialize(ref reader, options);
            return kind switch
            {
                MemberRefType.Other => default,
                MemberRefType.Field => this.DeserializeFieldReference(ref reader, options),
                MemberRefType.Property => this.DeserializePropertyReference(ref reader, options),
                MemberRefType.Method => this.DeserializeMethodReference(ref reader, options),
                _ => throw new NotSupportedException(),
            };
        }

        private TMemberReferenceType? DeserializeFieldReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out FieldRef? value, ref reader))
            {
                TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                TypeRef fieldType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                bool isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

                value = new FieldRef(declaringType, fieldType, metadataToken, name, isStatic);
                options.OnDeserializedReusableObject(id, value);
            }

            return value as TMemberReferenceType;
        }

        private TMemberReferenceType? DeserializePropertyReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out PropertyRef? value, ref reader))
            {
                TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                TypeRef propertyType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                bool isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                int? setter = options.Resolver.GetFormatterWithVerify<int?>().Deserialize(ref reader, options);
                int? getter = options.Resolver.GetFormatterWithVerify<int?>().Deserialize(ref reader, options);

                value = new PropertyRef(declaringType, propertyType, metadataToken, getter, setter, name, isStatic);
                options.OnDeserializedReusableObject(id, value);
            }

            return value as TMemberReferenceType;
        }

        private TMemberReferenceType? DeserializeMethodReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out MethodRef? value, ref reader))
            {
                TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                bool isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                ImmutableArray<TypeRef?> parameterTypes = TypeRefObjectFormatter.ReadTypeRefImmutableArray(ref reader, options);
                ImmutableArray<TypeRef?> genericMethodArguments = TypeRefObjectFormatter.ReadTypeRefImmutableArray(ref reader, options);

                value = new MethodRef(declaringType, metadataToken, name, isStatic, parameterTypes!, genericMethodArguments!);
                options.OnDeserializedReusableObject(id, value);
            }

            return value as TMemberReferenceType;
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, TMemberReferenceType? value, MessagePackSerializerOptions options)
        {
            switch (value)
            {
                case FieldRef fieldRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Field, options);
                    this.SerializeFieldReference(ref writer, fieldRef, options);
                    break;

                case PropertyRef propertyRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Property, options);
                    this.SerializePropertyReference(ref writer, propertyRef, options);
                    break;

                case MethodRef methodRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Method, options);
                    this.SerializeMethodReference(ref writer, methodRef, options);
                    break;

                default:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Other, options);
                    break;
            }
        }

        /// <summary>
        /// Serializes a field reference to the MessagePack writer.
        /// </summary>
        /// <param name="writer">The MessagePack writer.</param>
        /// <param name="value">The field reference to serialize.</param>
        /// <param name="options">The MessagePack serializer options.</param>
        private void SerializeFieldReference(ref MessagePackWriter writer, FieldRef value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.FieldTypeRef, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
            }
        }

        /// <summary>
        /// Serializes a method reference to the MessagePack writer.
        /// </summary>
        /// <param name="writer">The MessagePack writer.</param>
        /// <param name="value">The method reference to serialize.</param>
        /// <param name="options">The MessagePack serializer options.</param>
        private void SerializeMethodReference(ref MessagePackWriter writer, MethodRef value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
                MessagePackCollectionFormatter<TypeRef>.Instance.Serialize(ref writer, value.ParameterTypes, options);
                MessagePackCollectionFormatter<TypeRef>.Instance.Serialize(ref writer, value.GenericMethodArguments, options);
            }
        }

        /// <summary>
        /// Serializes a property reference to the MessagePack writer.
        /// </summary>
        /// <param name="writer">The MessagePack writer.</param>
        /// <param name="value">The property reference to serialize.</param>
        /// <param name="options">The MessagePack serializer options.</param>
        private void SerializePropertyReference(ref MessagePackWriter writer, PropertyRef value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
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
    }

    /// <summary>
    /// The possible types of member references.
    /// </summary>
    public enum MemberRefType
    {
        /// <summary>
        /// Represents a member reference of a type other than field, property, or method.
        /// </summary>
        Other,

        /// <summary>
        /// Represents a member reference of a field.
        /// </summary>
        Field,

        /// <summary>
        /// Represents a member reference of a property.
        /// </summary>
        Property,

        /// <summary>
        /// Represents a member reference of a method.
        /// </summary>
        Method,
    }
}
