// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;
using static Microsoft.VisualStudio.Composition.LazyMetadataWrapper;

internal class MetadataDictionaryFormatter : IMessagePackFormatter<IReadOnlyDictionary<string, object?>>
{
    internal static readonly MetadataDictionaryFormatter Instance = new();

    private MetadataDictionaryFormatter()
    {
    }

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, IReadOnlyDictionary<string, object?> value, MessagePackSerializerOptions options)
    {
        writer.WriteMapHeader(value.Count);

        // Special case certain values to avoid defeating lazy load later. Check out the
        // ReadMetadata below, how it wraps the return value.
        IReadOnlyDictionary<string, object?> serializedMetadata;

        // Unwrap the metadata if its an instance of LazyMetaDataWrapper, the wrapper may end up
        // implicitly resolving TypeRefs to Types which is undesirable.
        value = LazyMetadataWrapper.TryUnwrap(value);
        serializedMetadata = new LazyMetadataWrapper(value.ToImmutableDictionary(), LazyMetadataWrapper.Direction.ToSubstitutedValue, options.CompositionResolver());

        var stringFormatter = new Lazy<IMessagePackFormatter<string>>(() => options.Resolver.GetFormatterWithVerify<string>());
        var typeRefFormatter = new Lazy<IMessagePackFormatter<TypeRef?>>(() => options.Resolver.GetFormatterWithVerify<TypeRef?>());
        var guidFormatter = new Lazy<IMessagePackFormatter<Guid>>(() => options.Resolver.GetFormatterWithVerify<Guid>());
        var typeRefFormatterCollection = new Lazy<IMessagePackFormatter<IReadOnlyCollection<TypeRef?>>>(() => options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<TypeRef?>>());

        foreach (KeyValuePair<string, object?> item in serializedMetadata)
        {
            stringFormatter.Value.Serialize(ref writer, item.Key, options);
            SerializeObject(ref writer, item.Value);
        }

        void SerializeObject(ref MessagePackWriter messagePackWriter, object? value)
        {
            if (value is null)
            {
                messagePackWriter.WriteNil();
                return;
            }

            switch (value)
            {
                case Array array:
                    messagePackWriter.WriteArrayHeader(array.Length + 2); // +1 for the type of the array elementTypeRef
                    messagePackWriter.Write((byte)ObjectType.Array);
                    TypeRef? elementTypeRef = TypeRef.Get(value.GetType().GetElementType(), options.CompositionResolver());
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, elementTypeRef, options);
                    foreach (object? item in array)
                    {
                        SerializeObject(ref messagePackWriter, item);
                    }

                    break;

                case bool boolValue:
                    messagePackWriter.Write(boolValue);
                    break;

                case string stringValue:
                    stringFormatter.Value.Serialize(ref messagePackWriter, stringValue, options);
                    break;

                case long longValue:
                    messagePackWriter.WriteInt64(longValue);
                    break;

                case ulong ulongValue:
                    messagePackWriter.WriteUInt64(ulongValue);
                    break;

                case int intValue:
                    messagePackWriter.WriteInt32(intValue);
                    break;

                case uint uintValue:
                    messagePackWriter.WriteUInt32(uintValue);
                    break;

                case short shortValue:
                    messagePackWriter.WriteInt16(shortValue);
                    break;

                case ushort ushortValue:
                    messagePackWriter.WriteUInt16(ushortValue);
                    break;

                case byte byteValue:
                    messagePackWriter.WriteUInt8(byteValue);
                    break;

                case sbyte sbyteValue:
                    messagePackWriter.WriteInt8(sbyteValue);
                    break;

                case float floatValue:
                    messagePackWriter.Write(floatValue);
                    break;

                case double doubleValue:
                    messagePackWriter.Write(doubleValue);
                    break;

                case char charValue:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.Char);
                    messagePackWriter.WriteUInt16(charValue);
                    break;

                case Guid guidValue:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.Guid);
                    guidFormatter.Value.Serialize(ref messagePackWriter, guidValue, options);
                    break;

                case CreationPolicy creationPolicyValue:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.CreationPolicy);
                    messagePackWriter.WriteUInt8((byte)creationPolicyValue);
                    break;

                case TypeRef typeRefTypeValue:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.TypeRef);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, typeRefTypeValue, options);
                    break;

                case LazyMetadataWrapper.Enum32Substitution enum32SubstitutionValue:
                    messagePackWriter.WriteArrayHeader(3);
                    messagePackWriter.Write((byte)ObjectType.Enum32Substitution);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, enum32SubstitutionValue.EnumType, options);
                    options.Resolver.GetFormatterWithVerify<int?>().Serialize(ref messagePackWriter, enum32SubstitutionValue.RawValue, options);
                    break;

                case LazyMetadataWrapper.TypeSubstitution typeSubstitutionValue:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.TypeSubstitution);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, typeSubstitutionValue.TypeRef, options);
                    break;

                case LazyMetadataWrapper.TypeArraySubstitution typeArraySubstitutionValue:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.TypeArraySubstitution);
                    typeRefFormatterCollection.Value.Serialize(ref messagePackWriter, typeArraySubstitutionValue.TypeRefArray, options);

                    break;

                case Type objectType when typeof(Type).GetTypeInfo().IsAssignableFrom(objectType):
                    messagePackWriter.WriteArrayHeader(2);
                    TypeRef typeRefValue = TypeRef.Get((Type)value, options.CompositionResolver());
                    messagePackWriter.Write((byte)ObjectType.Type);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, typeRefValue, options);
                    break;
                default:
                    messagePackWriter.WriteArrayHeader(2);
                    messagePackWriter.Write((byte)ObjectType.TypeLess);
                    TypelessFormatter.Instance.Serialize(ref messagePackWriter, value, options);
                    break;
            }
        }
    }

    public IReadOnlyDictionary<string, object?> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        ImmutableDictionary<string, object?>.Builder builder = ImmutableDictionary.CreateBuilder<string, object?>();

        int count = reader.ReadMapHeader();
        ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty;

        var stringFormatter = new Lazy<IMessagePackFormatter<string>>(() => options.Resolver.GetFormatterWithVerify<string>());
        var typeRefFormatter = new Lazy<IMessagePackFormatter<TypeRef?>>(() => options.Resolver.GetFormatterWithVerify<TypeRef?>());
        var guidFormatter = new Lazy<IMessagePackFormatter<Guid>>(() => options.Resolver.GetFormatterWithVerify<Guid>());
        var typeRefFormatterCollection = new Lazy<IMessagePackFormatter<IReadOnlyList<TypeRef>>>(() => options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef>>());

        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                string? key = stringFormatter.Value.Deserialize(ref reader, options);
                object? value = DeserializeObject(ref reader);

                builder.Add(key, value);
            }

            metadata = builder.ToImmutable();
            builder.Clear();
        }

        return new LazyMetadataWrapper(metadata, LazyMetadataWrapper.Direction.ToOriginalValue, options.CompositionResolver());

        object? DeserializeObject(ref MessagePackReader messagePackReader)
        {
            if (messagePackReader.TryReadNil())
            {
                return null;
            }

            object? deserializedItem = null;
            MessagePackType messagePackType = messagePackReader.NextMessagePackType;

            deserializedItem = messagePackType switch
            {
                MessagePackType.Boolean => messagePackReader.ReadBoolean(),
                MessagePackType.Integer => messagePackReader.NextCode switch
                {
                    MessagePackCode.Int64 => messagePackReader.ReadInt64(),
                    MessagePackCode.Int32 => messagePackReader.ReadInt32(),
                    MessagePackCode.UInt64 => messagePackReader.ReadUInt64(),
                    MessagePackCode.UInt32 => messagePackReader.ReadUInt32(),
                    MessagePackCode.Int16 => messagePackReader.ReadInt16(),
                    MessagePackCode.UInt16 => messagePackReader.ReadUInt16(),
                    MessagePackCode.Int8 => messagePackReader.ReadSByte(),
                    MessagePackCode.UInt8 => messagePackReader.ReadByte(),
                    _ => DeserializeCustomObject(ref messagePackReader),
                },
                MessagePackType.Float => messagePackReader.NextCode switch
                {
                    MessagePackCode.Float32 => messagePackReader.ReadSingle(),
                    MessagePackCode.Float64 => messagePackReader.ReadDouble(),
                    _ => DeserializeCustomObject(ref messagePackReader),
                },
                MessagePackType.String => stringFormatter.Value.Deserialize(ref messagePackReader, options),
                _ => DeserializeCustomObject(ref messagePackReader),
            };
            return deserializedItem;

            object? DeserializeCustomObject(ref MessagePackReader messagePackReader)
            {
                int headerLength = messagePackReader.ReadArrayHeader();
                options.Security.DepthStep(ref messagePackReader);

                try
                {
                    object? deserializedValue;
                    var objectType = (ObjectType)messagePackReader.ReadByte();

                    switch (objectType)
                    {
                        case ObjectType.Array:

                            int arrayLength = headerLength;

                            Type elementType = typeRefFormatter.Value.Deserialize(ref messagePackReader, options).Resolve()!;
                            var arrayObject = Array.CreateInstance(elementType, (int)arrayLength - 2);

                            for (int i = 0; i < arrayObject.Length; i++)
                            {
                                object? valueToSet = DeserializeObject(ref messagePackReader);
                                arrayObject.SetValue(valueToSet, i);
                            }

                            deserializedValue = arrayObject;

                            break;

                        case ObjectType.Char:
                            deserializedValue = messagePackReader.ReadChar();
                            break;

                        case ObjectType.Guid:
                            deserializedValue = guidFormatter.Value.Deserialize(ref messagePackReader, options);
                            break;

                        case ObjectType.CreationPolicy:
                            deserializedValue = (CreationPolicy)messagePackReader.ReadByte();
                            break;

                        case ObjectType.Type:
                            deserializedValue = typeRefFormatter.Value.Deserialize(ref messagePackReader, options).Resolve();
                            break;

                        case ObjectType.TypeRef:
                            deserializedValue = typeRefFormatter.Value.Deserialize(ref messagePackReader, options);
                            break;

                        case ObjectType.Enum32Substitution:
                            TypeRef enumType = typeRefFormatter.Value.Deserialize(ref messagePackReader, options) ?? throw new MessagePackSerializationException($"Unexpected null for the type {nameof(Enum32Substitution)}");
                            int rawValue = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref messagePackReader, options);
                            deserializedValue = new LazyMetadataWrapper.Enum32Substitution(enumType, rawValue);
                            break;

                        case ObjectType.TypeSubstitution:
                            TypeRef typeRef = typeRefFormatter.Value.Deserialize(ref messagePackReader, options) ?? throw new MessagePackSerializationException($"Unexpected null for the type {nameof(TypeSubstitution)}");
                            deserializedValue = new LazyMetadataWrapper.TypeSubstitution(typeRef);
                            break;

                        case ObjectType.TypeArraySubstitution:
                            IReadOnlyList<TypeRef> typeRefArray = typeRefFormatterCollection.Value.Deserialize(ref messagePackReader, options);
                            deserializedValue = new LazyMetadataWrapper.TypeArraySubstitution(typeRefArray, options.CompositionResolver());
                            break;

                        case ObjectType.TypeLess:
                            deserializedValue = TypelessFormatter.Instance.Deserialize(ref messagePackReader, options);
                            break;

                        default:
                            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedFormat, objectType));
                    }

                    return deserializedValue;
                }
                finally
                {
                    messagePackReader.Depth--;
                }
            }
        }
    }

    private enum ObjectType : byte
    {
        CreationPolicy,
        Type,
        Array,
        TypeLess,
        TypeRef,
        Char,
        Guid,
        Enum32Substitution,
        TypeSubstitution,
        TypeArraySubstitution,
    }
}
