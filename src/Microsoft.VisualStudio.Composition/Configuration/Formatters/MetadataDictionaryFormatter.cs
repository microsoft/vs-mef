// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;

#pragma warning disable CS8604 // Possible null reference argument.

internal class MetadataDictionaryFormatter : IMessagePackFormatter<IReadOnlyDictionary<string, object?>>
{
    public static readonly MetadataDictionaryFormatter Instance = new();

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

            switch (value.GetType())
            {
                case Type objectType when objectType.IsArray:

                    var array = (Array)value;
                    messagePackWriter.Write((byte)ObjectType.Array);

                    TypeRef? elementTypeRef = TypeRef.Get(objectType.GetElementType(), options.CompositionResolver());
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, elementTypeRef, options);
                    messagePackWriter.Write(array.Length);
                    foreach (object? item in array)
                    {
                        SerializeObject(ref messagePackWriter, item);
                    }

                    break;

                case Type objectType when objectType == typeof(bool):
                    messagePackWriter.Write((bool)value);
                    break;

                case Type objectType when objectType == typeof(string):
                    stringFormatter.Value.Serialize(ref messagePackWriter, (string)value, options);
                    break;

                case Type objectType when objectType == typeof(long):
                    messagePackWriter.WriteInt64((long)value);
                    break;

                case Type objectType when objectType == typeof(ulong):
                    messagePackWriter.WriteUInt64((ulong)value);
                    break;

                case Type objectType when objectType == typeof(int):
                    messagePackWriter.WriteInt32((int)value);
                    break;

                case Type objectType when objectType == typeof(uint):
                    messagePackWriter.WriteUInt32((uint)value);
                    break;

                case Type objectType when objectType == typeof(short):
                    messagePackWriter.WriteInt16((short)value);
                    break;

                case Type objectType when objectType == typeof(ushort):
                    messagePackWriter.WriteUInt16((ushort)value);
                    break;

                case Type objectType when objectType == typeof(byte):
                    messagePackWriter.WriteUInt8((byte)value);
                    break;

                case Type objectType when objectType == typeof(sbyte):
                    messagePackWriter.WriteInt8((sbyte)value);
                    break;

                case Type objectType when objectType == typeof(float):
                    messagePackWriter.Write((float)value);
                    break;

                case Type objectType when objectType == typeof(double):
                    messagePackWriter.Write((double)value);
                    break;

                case Type objectType when objectType == typeof(char):
                    messagePackWriter.Write((byte)ObjectType.Char);
                    messagePackWriter.Write((char)value);
                    break;

                case Type objectType when objectType == typeof(Guid):
                    messagePackWriter.Write((byte)ObjectType.Guid);
                    guidFormatter.Value.Serialize(ref messagePackWriter, (Guid)value, options);
                    break;

                case Type objectType when objectType == typeof(CreationPolicy):
                    messagePackWriter.Write((byte)ObjectType.CreationPolicy);
                    messagePackWriter.Write((byte)(CreationPolicy)value);
                    break;

                case Type objectType when typeof(Type).GetTypeInfo().IsAssignableFrom(objectType):
                    TypeRef typeRefValue = TypeRef.Get((Type)value, options.CompositionResolver());
                    messagePackWriter.Write((byte)ObjectType.Type);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, typeRefValue, options);
                    break;

                case Type objectType when objectType == typeof(TypeRef):
                    messagePackWriter.Write((byte)ObjectType.TypeRef);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, (TypeRef)value, options);
                    break;

                case Type objectType when typeof(LazyMetadataWrapper.Enum32Substitution) == objectType:
                    var enum32SubstitutionValue = (LazyMetadataWrapper.Enum32Substitution)value;
                    messagePackWriter.Write((byte)ObjectType.Enum32Substitution);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, enum32SubstitutionValue.EnumType, options);
                    options.Resolver.GetFormatterWithVerify<int?>().Serialize(ref messagePackWriter, enum32SubstitutionValue.RawValue, options);
                    break;

                case Type objectType when typeof(LazyMetadataWrapper.TypeSubstitution) == objectType:
                    var typeSubstitutionValue = (LazyMetadataWrapper.TypeSubstitution)value;
                    messagePackWriter.Write((byte)ObjectType.TypeSubstitution);
                    typeRefFormatter.Value.Serialize(ref messagePackWriter, typeSubstitutionValue.TypeRef, options);
                    break;

                case Type objectType when typeof(LazyMetadataWrapper.TypeArraySubstitution) == objectType:
                    var typeArraySubstitutionValue = (LazyMetadataWrapper.TypeArraySubstitution)value;
                    messagePackWriter.Write((byte)ObjectType.TypeArraySubstitution);
                    typeRefFormatterCollection.Value.Serialize(ref messagePackWriter, typeArraySubstitutionValue.TypeRefArray, options);

                    break;

                default:
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
        var typeRefFormatterCollection = new Lazy<IMessagePackFormatter<IReadOnlyList<TypeRef?>>>(() => options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef?>>());

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

            object? response = null;
            MessagePackType messagePackType = messagePackReader.NextMessagePackType;

            response = messagePackType switch
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
            return response;

            object? DeserializeCustomObject(ref MessagePackReader messagePackReader)
            {
                object? response;
                var objectType = (ObjectType)options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref messagePackReader, options);

                switch (objectType)
                {
                    case ObjectType.Array:
                        Type elementType = typeRefFormatter.Value.Deserialize(ref messagePackReader, options).Resolve()!;

                        int arrayLength = messagePackReader.ReadInt32();
                        var arrayObject = Array.CreateInstance(elementType, (int)arrayLength);

                        for (int i = 0; i < arrayObject.Length; i++)
                        {
                            object? valueToSet = DeserializeObject(ref messagePackReader);
                            arrayObject.SetValue(valueToSet, i);
                        }

                        response = arrayObject;
                        break;

                    case ObjectType.Char:
                        response = messagePackReader.ReadChar();
                        break;

                    case ObjectType.Guid:
                        response = guidFormatter.Value.Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.CreationPolicy:
                        response = (CreationPolicy)messagePackReader.ReadByte();
                        break;

                    case ObjectType.Type:
                        response = typeRefFormatter.Value.Deserialize(ref messagePackReader, options).Resolve();
                        break;

                    case ObjectType.TypeRef:
                        response = typeRefFormatter.Value.Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Enum32Substitution:
                        TypeRef? enumType = typeRefFormatter.Value.Deserialize(ref messagePackReader, options);
                        int rawValue = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref messagePackReader, options);
                        response = new LazyMetadataWrapper.Enum32Substitution(enumType!, rawValue);
                        break;

                    case ObjectType.TypeSubstitution:
                        TypeRef? typeRef = typeRefFormatter.Value.Deserialize(ref messagePackReader, options);
                        response = new LazyMetadataWrapper.TypeSubstitution(typeRef!);
                        break;

                    case ObjectType.TypeArraySubstitution:
                        IReadOnlyList<TypeRef?> typeRefArray = typeRefFormatterCollection.Value.Deserialize(ref messagePackReader, options);
                        response = new LazyMetadataWrapper.TypeArraySubstitution(typeRefArray!, options.CompositionResolver());
                        break;

                    case ObjectType.TypeLess:
                        response = TypelessFormatter.Instance.Deserialize(ref messagePackReader, options);
                        break;

                    default:
                        throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedFormat, objectType));
                }

                return response;
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
