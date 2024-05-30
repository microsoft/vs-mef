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

    internal class FieldRefFormatter : IMessagePackFormatter<FieldRef?>
    {
        public static readonly FieldRefFormatter Instance = new();

        private FieldRefFormatter()
        {
        }

        public FieldRef? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

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

            return value;
        }

        public void Serialize(ref MessagePackWriter writer, FieldRef? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

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
    }
}
