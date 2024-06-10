// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;

internal class PropertyRefFormatter : IMessagePackFormatter<PropertyRef?>
{
    public static readonly PropertyRefFormatter Instance = new();

    private PropertyRefFormatter()
    {
    }

    public PropertyRef? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        options.Security.DepthStep(ref reader);

        try
        {
            var actualCount = reader.ReadArrayHeader();
            if (actualCount != 7)
            {
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(PropertyRef)}. Expected: {7}, Actual: {actualCount}");
            }

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            TypeRef declaringType = typeRefFormatter.Deserialize(ref reader, options);
            TypeRef propertyType = typeRefFormatter.Deserialize(ref reader, options);
            int metadataToken = reader.ReadInt32();
            string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            bool isStatic = reader.ReadBoolean();
            int? setter = null;
            int? getter = null;
            if (!reader.TryReadNil())
            {
                setter = reader.ReadInt32();
            }

            if (!reader.TryReadNil())
            {
                getter = reader.ReadInt32();
            }

            return new PropertyRef(declaringType, propertyType, metadataToken, getter, setter, name, isStatic);
        }
        finally
        {
            reader.Depth--;
        }
    }

    public void Serialize(ref MessagePackWriter writer, PropertyRef? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(7);

        IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

        typeRefFormatter.Serialize(ref writer, value.DeclaringType, options);
        typeRefFormatter.Serialize(ref writer, value.PropertyTypeRef, options);

        writer.Write(value.MetadataToken);
        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
        writer.Write(value.IsStatic);
        if (value.SetMethodMetadataToken.HasValue)
        {
            writer.Write(value.SetMethodMetadataToken.Value);
        }
        else
        {
            writer.WriteNil();
        }

        if (value.GetMethodMetadataToken.HasValue)
        {
            writer.Write(value.GetMethodMetadataToken.Value);
        }
        else
        {
            writer.WriteNil();
        }
    }
}
