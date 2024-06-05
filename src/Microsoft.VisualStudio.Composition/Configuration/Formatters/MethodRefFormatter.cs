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
             : base(arrayElementCount: 6)
        {
        }

        protected override MethodRef? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

            int metadataToken = reader.ReadInt32();
            string name = reader.ReadString()!;
            bool isStatic = reader.ReadBoolean();

            IMessagePackFormatter<ImmutableArray<TypeRef?>> typeRefFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef?>>();

            ImmutableArray<TypeRef?> parameterTypes = typeRefFormatter.Deserialize(ref reader, options);
            ImmutableArray<TypeRef?> genericMethodArguments = typeRefFormatter.Deserialize(ref reader, options);

            return new MethodRef(declaringType, metadataToken, name, isStatic, parameterTypes!, genericMethodArguments!);
        }

        protected override void SerializeData(ref MessagePackWriter writer, MethodRef? value, MessagePackSerializerOptions options)
        {
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
