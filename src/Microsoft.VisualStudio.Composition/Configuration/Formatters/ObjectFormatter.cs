// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    //#pragma warning disable CS8602 // possible dereference of null reference
    //#pragma warning disable CS8604 // null reference as argument

    public class ObjectFormatter : IMessagePackFormatter<IReadOnlyDictionary<string, object?>>
    {
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object?> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return DeserializeObject(ref reader, options);
        }

        public void Serialize(ref MessagePackWriter writer, IReadOnlyDictionary<string, object?> value, MessagePackSerializerOptions options)
        {
            SerializeObject(ref writer, value, options);
        }

        internal static void SerializeObject(ref MessagePackWriter writer, IReadOnlyDictionary<string, object?> value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.Count(), options);

            foreach (KeyValuePair<string, object?> item in value)
            {
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, item.Key, options);

                SerializeObject(ref writer, item.Value);
            }

            void SerializeObject(ref MessagePackWriter messagePackWriter, object? value)
            {
                if (value is null)
                {
                    options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectFormatter.ObjectType.Null, options);
                    return;
                }

                switch (value.GetType())
                {
                    case Type objectType when objectType.IsArray:

                        var array = (Array)value;
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectFormatter.ObjectType.Array, options);

                        TypeRef? elementTypeRef = TypeRef.Get(objectType.GetElementType(), ResolverFormatterContainer.Resolver);
                        options.Resolver.GetFormatterWithVerify<TypeRef?>().Serialize(ref messagePackWriter, elementTypeRef, options);

                        options.Resolver.GetFormatterWithVerify<int>().Serialize(ref messagePackWriter, array.Length, options);
                        foreach (object? item in array)
                        {
                            SerializeObject(ref messagePackWriter, item);
                        }

                        break;

                    case Type objectType when objectType == typeof(bool):
                        ObjectType objectValueType = (bool)value ? ObjectType.BoolTrue : ObjectType.BoolFalse;
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)objectValueType, options);
                        break;

                    case Type objectType when objectType == typeof(string):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.String, options);
                        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref messagePackWriter, (string)value, options);
                        break;

                    case Type objectType when objectType == typeof(long):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Int64, options);
                        options.Resolver.GetFormatterWithVerify<long>().Serialize(ref messagePackWriter, (long)value, options);
                        break;

                    case Type objectType when objectType == typeof(ulong):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.UInt64, options);
                        options.Resolver.GetFormatterWithVerify<ulong>().Serialize(ref messagePackWriter, (ulong)value, options);
                        break;

                    case Type objectType when objectType == typeof(int):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Int32, options);
                        options.Resolver.GetFormatterWithVerify<int>().Serialize(ref messagePackWriter, (int)value, options);
                        break;

                    case Type objectType when objectType == typeof(uint):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.UInt32, options);
                        options.Resolver.GetFormatterWithVerify<uint>().Serialize(ref messagePackWriter, (uint)value, options);
                        break;

                    case Type objectType when objectType == typeof(short):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Int16, options);
                        options.Resolver.GetFormatterWithVerify<short>().Serialize(ref messagePackWriter, (short)value, options);
                        break;

                    case Type objectType when objectType == typeof(ushort):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.UInt16, options);
                        options.Resolver.GetFormatterWithVerify<ushort>().Serialize(ref messagePackWriter, (ushort)value, options);
                        break;

                    case Type objectType when objectType == typeof(byte):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Byte, options);
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)value, options);
                        break;

                    case Type objectType when objectType == typeof(sbyte):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.SByte, options);
                        options.Resolver.GetFormatterWithVerify<sbyte>().Serialize(ref messagePackWriter, (sbyte)value, options);
                        break;

                    case Type objectType when objectType == typeof(float):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Single, options);
                        options.Resolver.GetFormatterWithVerify<float>().Serialize(ref messagePackWriter, (float)value, options);
                        break;

                    case Type objectType when objectType == typeof(double):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Double, options);
                        options.Resolver.GetFormatterWithVerify<double>().Serialize(ref messagePackWriter, (double)value, options);
                        break;

                    case Type objectType when objectType == typeof(char):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Char, options);
                        options.Resolver.GetFormatterWithVerify<char>().Serialize(ref messagePackWriter, (char)value, options);
                        break;

                    case Type objectType when objectType == typeof(Guid):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Guid, options);
                        options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref messagePackWriter, (Guid)value, options);
                        break;

                    case Type objectType when objectType == typeof(CreationPolicy):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.CreationPolicy, options);
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)(CreationPolicy)value, options);
                        break;

                    case Type objectType when typeof(Type).GetTypeInfo().IsAssignableFrom(objectType):
                        TypeRef typeRefValue = TypeRef.Get((Type)value, ResolverFormatterContainer.Resolver);
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Type, options);
                        options.Resolver.GetFormatterWithVerify<TypeRef?>().Serialize(ref messagePackWriter, typeRefValue, options);
                        break;

                    case Type objectType when objectType == typeof(TypeRef):
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.TypeRef, options);
                        options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref messagePackWriter, (TypeRef)value, options);
                        break;

                    case Type objectType when typeof(LazyMetadataWrapper.Enum32Substitution) == objectType:
                        var enum32SubstitutionValue = (LazyMetadataWrapper.Enum32Substitution)value;
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.Enum32Substitution, options);
                        options.Resolver.GetFormatterWithVerify<TypeRef?>().Serialize(ref messagePackWriter, enum32SubstitutionValue.EnumType, options);
                        options.Resolver.GetFormatterWithVerify<int?>().Serialize(ref messagePackWriter, enum32SubstitutionValue.RawValue, options);
                        break;

                    case Type objectType when typeof(LazyMetadataWrapper.TypeSubstitution) == objectType:
                        var typeSubstitutionValue = (LazyMetadataWrapper.TypeSubstitution)value;
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.TypeSubstitution, options);
                        options.Resolver.GetFormatterWithVerify<TypeRef?>().Serialize(ref messagePackWriter, typeSubstitutionValue.TypeRef, options);
                        break;

                    case Type objectType when typeof(LazyMetadataWrapper.TypeArraySubstitution) == objectType:
                        var typeArraySubstitutionValue = (LazyMetadataWrapper.TypeArraySubstitution)value;
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.TypeArraySubstitution, options);
                        options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef>>().Serialize(ref messagePackWriter, typeArraySubstitutionValue.TypeRefArray, options);
                        break;

                    default:
                        // work on this we need to remove this
                        var typeLessData = MessagePackSerializer.Typeless.Serialize(value);
                        options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref messagePackWriter, (byte)ObjectType.BinaryFormattedObject, options);
                        options.Resolver.GetFormatterWithVerify<byte[]>().Serialize(ref messagePackWriter, typeLessData, options);
                        break;
                }
            }
        }
        internal static IReadOnlyDictionary<string, object?> DeserializeObject(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            ImmutableDictionary<string, object?>.Builder builder = ImmutableDictionary.CreateBuilder<string, object?>();

            int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
            ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty;

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    string key = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    object? value = DeserializeObject(ref reader);

                    builder.Add(key, value);
                }

                metadata = builder.ToImmutable();
                builder.Clear();
            }

            return new LazyMetadataWrapper(metadata, LazyMetadataWrapper.Direction.ToOriginalValue, ResolverFormatterContainer.Resolver);

            object? DeserializeObject(ref MessagePackReader messagePackReader)
            {
                var objectType = (ObjectType)options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref messagePackReader, options);

                object? response = null;

                switch (objectType)
                {
                    case ObjectType.Null:
                        response = null;
                        break;

                    case ObjectType.Array:
                        Type elementType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref messagePackReader, options).Resolve();

                        int arrayLength = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref messagePackReader, options);
                        var arrayObject = Array.CreateInstance(elementType, (int)arrayLength);

                        for (int i = 0; i < arrayObject.Length; i++)
                        {
                            object? valueToSet = DeserializeObject(ref messagePackReader);
                            arrayObject.SetValue(valueToSet, i);
                        }

                        response = arrayObject;
                        break;

                    case ObjectType.BoolTrue:
                        response = true;
                        break;

                    case ObjectType.BoolFalse:
                        response = false;
                        break;

                    case ObjectType.UInt64:
                        response = options.Resolver.GetFormatterWithVerify<ulong>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Int32:
                        response = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.UInt32:
                        response = options.Resolver.GetFormatterWithVerify<uint>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Int16:
                        response = options.Resolver.GetFormatterWithVerify<short>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.UInt16:
                        response = options.Resolver.GetFormatterWithVerify<ushort>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Int64:
                        response = options.Resolver.GetFormatterWithVerify<long>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Byte:
                        response = options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.SByte:
                        response = options.Resolver.GetFormatterWithVerify<sbyte>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Single:
                        response = options.Resolver.GetFormatterWithVerify<float>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Double:
                        response = options.Resolver.GetFormatterWithVerify<double>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.String:
                        response = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Char:
                        response = options.Resolver.GetFormatterWithVerify<char>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Guid:
                        response = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.CreationPolicy:
                        response = (CreationPolicy)options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Type:
                        response = options.Resolver.GetFormatterWithVerify<TypeRef?>().Deserialize(ref messagePackReader, options).Resolve();
                        break;

                    case ObjectType.TypeRef:
                        response = options.Resolver.GetFormatterWithVerify<TypeRef?>().Deserialize(ref messagePackReader, options);
                        break;

                    case ObjectType.Enum32Substitution:
                        TypeRef? enumType = options.Resolver.GetFormatterWithVerify<TypeRef?>().Deserialize(ref messagePackReader, options);
                        int rawValue = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref messagePackReader, options);
                        response = new LazyMetadataWrapper.Enum32Substitution(enumType, rawValue);
                        break;

                    case ObjectType.TypeSubstitution:
                        TypeRef? typeRef = options.Resolver.GetFormatterWithVerify<TypeRef?>().Deserialize(ref messagePackReader, options);
                        response = new LazyMetadataWrapper.TypeSubstitution(typeRef);
                        break;

                    case ObjectType.TypeArraySubstitution:
                        IReadOnlyList<TypeRef?> typeRefArray = options.Resolver.GetFormatterWithVerify<IReadOnlyList<TypeRef?>>().Deserialize(ref messagePackReader, options);
                        response = new LazyMetadataWrapper.TypeArraySubstitution(typeRefArray!, ResolverFormatterContainer.Resolver);
                        break;

                    case ObjectType.BinaryFormattedObject:
                        //to do binary fomratter
                        //response = options.Resolver.GetFormatterWithVerify<object>().Deserialize(ref messagePackReader, options);
                        var typeLessData = options.Resolver.GetFormatterWithVerify<byte[]>().Deserialize(ref messagePackReader, options);

                        response = MessagePackSerializer.Typeless.Deserialize(typeLessData);

                        break;
                    default:
                        throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedFormat, objectType));
                }

                return response;
            }
        }


        private enum ObjectType : byte
        {
            Null,
            String,
            CreationPolicy,
            Type,
            Array,
            BinaryFormattedObject,
            TypeRef,
            BoolTrue,
            BoolFalse,
            Int32,
            Char,
            Guid,
            Enum32Substitution,
            TypeSubstitution,
            TypeArraySubstitution,
            Single,
            Double,
            UInt16,
            Int64,
            UInt64,
            Int16,
            UInt32,
            Byte,
            SByte,
        }
    }
}
