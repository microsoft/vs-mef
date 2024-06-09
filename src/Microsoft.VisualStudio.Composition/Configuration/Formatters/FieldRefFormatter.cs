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

            options.Security.DepthStep(ref reader);

            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 5)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(FieldRef)}. Expected: {5}, Actual: {actualCount}");
                }

                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                TypeRef declaringType = typeRefFormatter.Deserialize(ref reader, options);
                TypeRef fieldType = typeRefFormatter.Deserialize(ref reader, options);
                int metadataToken = reader.ReadInt32();
                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                bool isStatic = reader.ReadBoolean();

                return new FieldRef(declaringType, fieldType, metadataToken, name, isStatic);
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, FieldRef? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(5);

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            typeRefFormatter.Serialize(ref writer, value!.DeclaringType, options);
            typeRefFormatter.Serialize(ref writer, value!.FieldTypeRef, options);
            writer.Write(value.MetadataToken);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
            writer.Write(value.IsStatic);
        }
    }
}
