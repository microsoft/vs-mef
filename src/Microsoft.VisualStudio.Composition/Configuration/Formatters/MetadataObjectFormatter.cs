// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;
using static Microsoft.VisualStudio.Composition.LazyMetadataWrapper;

internal class MetadataObjectFormatter(Resolver compositionResolver) : IMessagePackFormatter<object?>
{
    public void Serialize(ref MessagePackWriter messagePackWriter, object? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            messagePackWriter.WriteNil();
            return;
        }

        var stringFormatter = options.Resolver.GetFormatterWithVerify<string>();
        var typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef?>();
        var guidFormatter = options.Resolver.GetFormatterWithVerify<Guid>();
        var typeRefFormatterCollection = options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef>>();

        switch (value)
        {
            case Array array:
                messagePackWriter.WriteArrayHeader(3);

                messagePackWriter.Write((byte)ObjectType.Array);
                TypeRef? elementTypeRef = TypeRef.Get(value.GetType().GetElementType(), compositionResolver);
                typeRefFormatter.Serialize(ref messagePackWriter, elementTypeRef, options);

                messagePackWriter.WriteArrayHeader(array.Length);
                foreach (object? item in array)
                {
                    this.Serialize(ref messagePackWriter, item, options);
                }

                break;

            case bool boolValue:
                messagePackWriter.Write(boolValue);
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

            case string stringValue:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.String);
                stringFormatter.Serialize(ref messagePackWriter, stringValue, options);
                break;

            case Guid guidValue:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.Guid);
                guidFormatter.Serialize(ref messagePackWriter, guidValue, options);
                break;

            case CreationPolicy creationPolicyValue:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.CreationPolicy);
                messagePackWriter.WriteUInt8((byte)creationPolicyValue);
                break;

            case TypeRef typeRefTypeValue:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.TypeRef);
                typeRefFormatter.Serialize(ref messagePackWriter, typeRefTypeValue, options);
                break;

            case LazyMetadataWrapper.Enum32Substitution enum32SubstitutionValue:
                messagePackWriter.WriteArrayHeader(3);
                messagePackWriter.Write((byte)ObjectType.Enum32Substitution);
                typeRefFormatter.Serialize(ref messagePackWriter, enum32SubstitutionValue.EnumType, options);
                options.Resolver.GetFormatterWithVerify<int?>().Serialize(ref messagePackWriter, enum32SubstitutionValue.RawValue, options);
                break;

            case LazyMetadataWrapper.TypeSubstitution typeSubstitutionValue:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.TypeSubstitution);
                typeRefFormatter.Serialize(ref messagePackWriter, typeSubstitutionValue.TypeRef, options);
                break;

            case LazyMetadataWrapper.TypeArraySubstitution typeArraySubstitutionValue:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.TypeArraySubstitution);
                typeRefFormatterCollection.Serialize(ref messagePackWriter, typeArraySubstitutionValue.TypeRefArray, options);

                break;

            case Type objectType when typeof(Type).GetTypeInfo().IsAssignableFrom(objectType):
                messagePackWriter.WriteArrayHeader(2);
                TypeRef typeRefValue = TypeRef.Get((Type)value, compositionResolver);
                messagePackWriter.Write((byte)ObjectType.Type);
                typeRefFormatter.Serialize(ref messagePackWriter, typeRefValue, options);
                break;
            default:
                messagePackWriter.WriteArrayHeader(2);
                messagePackWriter.Write((byte)ObjectType.Typeless);
                TypelessFormatter.Instance.Serialize(ref messagePackWriter, value, options);
                break;
        }
    }

    public ImmutableDictionary<string, object?> DeserializeMetadataObjects(ref MessagePackReader reader, MessagePackSerializerOptions options, int count)
    {
        var dictionary = new Dictionary<string, object?>(count);

        try
        {
            var stringFormatter = options.Resolver.GetFormatterWithVerify<string>();
            var typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef?>();
            var guidFormatter = options.Resolver.GetFormatterWithVerify<Guid>();
            var typeRefFormatterCollection = options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef>>();

            for (int i = 0; i < count; i++)
            {
                dictionary.Add(stringFormatter.Deserialize(ref reader, options), this.Deserialize(ref reader, options, stringFormatter, typeRefFormatter, guidFormatter, typeRefFormatterCollection));
            }
        }
        finally
        {
            reader.Depth--;
        }

        return dictionary.ToImmutableDictionary();
    }

    public object? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return this.Deserialize(ref reader, options, options.Resolver.GetFormatterWithVerify<string>(), options.Resolver.GetFormatterWithVerify<TypeRef?>(), options.Resolver.GetFormatterWithVerify<Guid>(), options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef>>());
    }

    private object? Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options,
        IMessagePackFormatter<string> stringFormatter,
        IMessagePackFormatter<TypeRef?> typeRefFormatter,
        IMessagePackFormatter<Guid> guidFormatter,
        IMessagePackFormatter<IReadOnlyList<TypeRef>> typeRefFormatterCollection)
    {
        return DeserializeObject(ref reader);

        object? DeserializeObject(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            return reader.NextMessagePackType switch
            {
                MessagePackType.Boolean => reader.ReadBoolean(),
                MessagePackType.Integer => ReadInteger(ref reader),
                MessagePackType.Float => ReadFloat(ref reader),
                _ => DeserializeCustomObject(ref reader),
            };
        }

        object? ReadFloat(ref MessagePackReader reader) => reader.NextCode switch
        {
            MessagePackCode.Float32 => reader.ReadSingle(),
            MessagePackCode.Float64 => reader.ReadDouble(),
            _ => DeserializeCustomObject(ref reader),
        };

        object ReadInteger(ref MessagePackReader reader) => reader.NextCode switch
        {
            MessagePackCode.Int64 => reader.ReadInt64(),
            MessagePackCode.Int32 => reader.ReadInt32(),
            MessagePackCode.UInt64 => reader.ReadUInt64(),
            MessagePackCode.UInt32 => reader.ReadUInt32(),
            MessagePackCode.Int16 => reader.ReadInt16(),
            MessagePackCode.UInt16 => reader.ReadUInt16(),
            MessagePackCode.Int8 => reader.ReadSByte(),
            MessagePackCode.UInt8 => reader.ReadByte(),
            _ => throw new MessagePackSerializationException("Unexpected integer type"),
        };

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
                        Type elementType = typeRefFormatter.Deserialize(ref messagePackReader, options).Resolve()!;

                        int arrayLength = messagePackReader.ReadArrayHeader();
                        var arrayObject = Array.CreateInstance(elementType, arrayLength);

                        if (arrayObject is object[] objectArray)
                        {
                            for (int i = 0; i < arrayLength; i++)
                            {
                                objectArray[i] = DeserializeObject(ref messagePackReader)!;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < arrayLength; i++)
                            {
                                arrayObject.SetValue(DeserializeObject(ref messagePackReader), i);
                            }
                        }

                        deserializedValue = arrayObject;

                        break;

                    case ObjectType.String:
                        deserializedValue = stringFormatter.Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Char:
                        deserializedValue = messagePackReader.ReadChar();
                        break;

                    case ObjectType.Guid:
                        deserializedValue = guidFormatter.Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.CreationPolicy:
                        deserializedValue = (CreationPolicy)messagePackReader.ReadByte();
                        break;

                    case ObjectType.Type:
                        deserializedValue = typeRefFormatter.Deserialize(ref messagePackReader, options).Resolve();
                        break;

                    case ObjectType.TypeRef:
                        deserializedValue = typeRefFormatter.Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Enum32Substitution:
                        TypeRef enumType = typeRefFormatter.Deserialize(ref messagePackReader, options) ?? throw new MessagePackSerializationException($"Unexpected null for the type {nameof(Enum32Substitution)}");
                        int rawValue = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref messagePackReader, options);
                        deserializedValue = new LazyMetadataWrapper.Enum32Substitution(enumType, rawValue);
                        break;

                    case ObjectType.TypeSubstitution:
                        TypeRef typeRef = typeRefFormatter.Deserialize(ref messagePackReader, options) ?? throw new MessagePackSerializationException($"Unexpected null for the type {nameof(TypeSubstitution)}");
                        deserializedValue = new LazyMetadataWrapper.TypeSubstitution(typeRef);
                        break;

                    case ObjectType.TypeArraySubstitution:
                        IReadOnlyList<TypeRef> typeRefArray = typeRefFormatterCollection.Deserialize(ref messagePackReader, options);
                        deserializedValue = new LazyMetadataWrapper.TypeArraySubstitution(typeRefArray, compositionResolver);
                        break;

                    case ObjectType.Typeless:
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
