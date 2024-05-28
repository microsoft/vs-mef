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
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                TypeRef declaringType = typeRefFormatter.Deserialize(ref reader, options);
                TypeRef fieldType = typeRefFormatter.Deserialize(ref reader, options);
                int metadataToken = reader.ReadInt32();
                string name = reader.ReadString()!;
                bool isStatic = reader.ReadBoolean();

                value = new FieldRef(declaringType, fieldType, metadataToken, name, isStatic);
                options.OnDeserializedReusableObject(id, value);
            }

            return value as TMemberReferenceType;
        }

        private TMemberReferenceType? DeserializePropertyReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out PropertyRef? value, ref reader))
            {
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                TypeRef declaringType = typeRefFormatter.Deserialize(ref reader, options);
                TypeRef propertyType = typeRefFormatter.Deserialize(ref reader, options);
                int metadataToken = reader.ReadInt32();
                string name = reader.ReadString()!;
                bool isStatic = reader.ReadBoolean();
                int? setter = null;
                int? getter = null;
                if (reader.ReadBoolean())
                {
                    setter = reader.ReadInt32();
                }

                if (reader.ReadBoolean())
                {
                    getter = reader.ReadInt32();
                }

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

                int metadataToken = reader.ReadInt32();
                string name = reader.ReadString()!;
                bool isStatic = reader.ReadBoolean();

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
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                typeRefFormatter.Serialize(ref writer, value.DeclaringType, options);
                typeRefFormatter.Serialize(ref writer, value.FieldTypeRef, options);
                writer.Write(value.MetadataToken);
                writer.Write(value.Name);
                writer.Write(value.IsStatic);
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
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                typeRefFormatter.Serialize(ref writer, value.DeclaringType, options);

                writer.Write(value.MetadataToken);
                writer.Write(value.Name);
                writer.Write(value.IsStatic);

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
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                typeRefFormatter.Serialize(ref writer, value.DeclaringType, options);
                typeRefFormatter.Serialize(ref writer, value.PropertyTypeRef, options);

                writer.Write(value.MetadataToken);
                writer.Write(value.Name);
                writer.Write(value.IsStatic);
                if (value.SetMethodMetadataToken.HasValue)
                {
                    writer.Write(true);
                    writer.Write(value.SetMethodMetadataToken.Value);
                }
                else
                {
                    writer.Write(false);
                }

                if (value.GetMethodMetadataToken.HasValue)
                {
                    writer.Write(true);
                    writer.Write(value.GetMethodMetadataToken.Value);
                }
                else
                {
                    writer.Write(false);
                }
            }
        }
    }

    /// <summary>
    /// The possible types of member references.
    /// </summary>
    internal enum MemberRefType
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
