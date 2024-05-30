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

    internal class MethodRefFormatter : BaseMessagePackFormatter<MethodRef?>
    {
        public static readonly MethodRefFormatter Instance = new();

        private MethodRefFormatter()
        {
        }

        protected override MethodRef? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out MethodRef? value, ref reader))
            {
                this.CheckArrayHeaderCount(ref reader, 6);
                TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

                int metadataToken = reader.ReadInt32();
                string name = reader.ReadString()!;
                bool isStatic = reader.ReadBoolean();

                IMessagePackFormatter<ImmutableArray<TypeRef?>> typeRefFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef?>>();

                ImmutableArray<TypeRef?> parameterTypes = typeRefFormatter.Deserialize(ref reader, options);
                ImmutableArray<TypeRef?> genericMethodArguments = typeRefFormatter.Deserialize(ref reader, options);

                value = new MethodRef(declaringType, metadataToken, name, isStatic, parameterTypes!, genericMethodArguments!);
                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        protected override void SerializeData(ref MessagePackWriter writer, MethodRef? value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                writer.WriteArrayHeader(6);
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                typeRefFormatter.Serialize(ref writer, value!.DeclaringType, options);

                writer.Write(value.MetadataToken);
                writer.Write(value.Name);
                writer.Write(value.IsStatic);

                IMessagePackFormatter<ImmutableArray<TypeRef>> typeREfFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>();
                typeREfFormatter.Serialize(ref writer, value.ParameterTypes, options);
                typeREfFormatter.Serialize(ref writer, value.GenericMethodArguments, options);
            }
        }
    }
}
