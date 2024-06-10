// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class MethodRefFormatter : IMessagePackFormatter<MethodRef?>
    {
        public static readonly MethodRefFormatter Instance = new();

        private MethodRefFormatter()
        {
        }

        public MethodRef? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);

            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 6)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(MethodRef)}. Expected: {6}, Actual: {actualCount}");
                }

                TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

                int metadataToken = reader.ReadInt32();
                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                bool isStatic = reader.ReadBoolean();

                IMessagePackFormatter<ImmutableArray<TypeRef?>> typeRefFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef?>>();

                ImmutableArray<TypeRef?> parameterTypes = typeRefFormatter.Deserialize(ref reader, options);
                ImmutableArray<TypeRef?> genericMethodArguments = typeRefFormatter.Deserialize(ref reader, options);

                return new MethodRef(declaringType, metadataToken, name, isStatic, parameterTypes!, genericMethodArguments!);
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, MethodRef? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(6);

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            typeRefFormatter.Serialize(ref writer, value!.DeclaringType, options);

            writer.Write(value.MetadataToken);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
            writer.Write(value.IsStatic);

            IMessagePackFormatter<ImmutableArray<TypeRef>> typeREfFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>();
            typeREfFormatter.Serialize(ref writer, value.ParameterTypes, options);
            typeREfFormatter.Serialize(ref writer, value.GenericMethodArguments, options);
        }
    }
}
