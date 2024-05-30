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

    internal class PropertyRefFormatter : BaseMessagePackFormatter<PropertyRef?>
    {
        public static readonly PropertyRefFormatter Instance = new();

        private PropertyRefFormatter()
        {
        }

        protected override PropertyRef? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out PropertyRef? value, ref reader))
            {
                this.CheckArrayHeaderCount(ref reader, 7);

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

            return value;
        }

        protected override void SerializeData(ref MessagePackWriter writer, PropertyRef? value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                writer.WriteArrayHeader(7);
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                typeRefFormatter.Serialize(ref writer, value!.DeclaringType, options);
                typeRefFormatter.Serialize(ref writer, value!.PropertyTypeRef, options);

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
}
