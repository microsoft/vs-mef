// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.VisualStudio.Composition;

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class MemberRefFormatter<MemberReferenceType> : IMessagePackFormatter<MemberReferenceType>
        where MemberReferenceType : MemberRef
    {
        /// <inheritdoc/>
        public MemberReferenceType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            MemberRefType kind = options.Resolver.GetFormatterWithVerify<MemberRefType>().Deserialize(ref reader, options);

            switch (kind)
            {
                case MemberRefType.Other:
                    return default(MemberRef) as MemberReferenceType;

                case MemberRefType.Field:
                    return DeserializeFieldReference(ref reader, options);

                case MemberRefType.Property:
                    return DeserializePropertyReference(ref reader, options);

                case MemberRefType.Method:
                    return DeserializeMethodReference(ref reader, options);

                default:
                    throw new NotSupportedException();
            }

            MemberReferenceType DeserializeFieldReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (MessagePackFormatterContext.TryPrepareDeserializeReusableObject(out uint id, out FieldRef? value, ref reader, options))
                {


                    TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                    TypeRef fieldType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                    int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                    string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    bool isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

                    value = new FieldRef(declaringType, fieldType, metadataToken, name, isStatic);
                    //return value as MemberReferenceType;

                    MessagePackFormatterContext.OnDeserializedReusableObject(id, value);
                }

                return value as MemberReferenceType;

            }

            MemberReferenceType DeserializePropertyReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (MessagePackFormatterContext.TryPrepareDeserializeReusableObject(out uint id, out PropertyRef? value, ref reader, options))
                {

                    TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                    TypeRef propertyType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                    int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                    string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    bool isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                    int? setter = options.Resolver.GetFormatterWithVerify<int?>().Deserialize(ref reader, options);
                    int? getter = options.Resolver.GetFormatterWithVerify<int?>().Deserialize(ref reader, options);

                    value = new PropertyRef(declaringType, propertyType, metadataToken, getter, setter, name, isStatic);
                    MessagePackFormatterContext.OnDeserializedReusableObject(id, value);
                }

                return value as MemberReferenceType;

               
            }

            MemberReferenceType DeserializeMethodReference(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (MessagePackFormatterContext.TryPrepareDeserializeReusableObject(out uint id, out MethodRef? value, ref reader, options))
                {
                    TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                    int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                    string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    bool isStatic = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                    ImmutableArray<TypeRef?> parameterTypes = TypeRefObjectFormatter.ReadTypeRefImmutableArray(ref reader, options);
                    ImmutableArray<TypeRef?> genericMethodArguments = TypeRefObjectFormatter.ReadTypeRefImmutableArray(ref reader, options);

                    value = new MethodRef(declaringType, metadataToken, name, isStatic, parameterTypes!, genericMethodArguments!);

                    MessagePackFormatterContext.OnDeserializedReusableObject(id, value);
                }

                return value as MemberReferenceType;
            }
        }

        public void Serialize(ref MessagePackWriter writer, MemberReferenceType value, MessagePackSerializerOptions options)
        {
            switch (value)
            {
                case FieldRef fieldRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Field, options);
                    SerializeFieldReference(ref writer, fieldRef, options);
                    break;

                case PropertyRef propertyRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Property, options);
                    SerializePropertyReference(ref writer, propertyRef, options);

                    break;

                case MethodRef methodRef:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Method, options);
                    SerializeMethodReference(ref writer, methodRef, options);

                    break;

                default:
                    options.Resolver.GetFormatterWithVerify<MemberRefType>().Serialize(ref writer, MemberRefType.Other, options);

                    break;
            }

            void SerializeFieldReference(ref MessagePackWriter writer, FieldRef value, MessagePackSerializerOptions options)
            {
                if (MessagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, options))
                {
                    options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                    options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.FieldTypeRef, options);
                    options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                    options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                    options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
                }
            }

            void SerializeMethodReference(ref MessagePackWriter writer, MethodRef value, MessagePackSerializerOptions options)
            {
                if (MessagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, options))
                {
                    options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringType, options);
                    options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
                    options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
                    options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsStatic, options);
                    CollectionFormatter<TypeRef>.SerializeCollection(ref writer, value.ParameterTypes, options); //todo
                    CollectionFormatter<TypeRef>.SerializeCollection(ref writer, value.GenericMethodArguments, options); //todo
                }
            }

            void SerializePropertyReference(ref MessagePackWriter writer, PropertyRef value, MessagePackSerializerOptions options)
            {
                if (MessagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, options))
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

        public enum MemberRefType
        {
            Other = 0,
            Field,
            Property,
            Method,
        }
    }
}
