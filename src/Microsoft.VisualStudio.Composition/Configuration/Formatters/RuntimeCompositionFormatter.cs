// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeCompositionFormatter : IMessagePackFormatter<RuntimeComposition>
    {
        public static readonly RuntimeCompositionFormatter Instance = new();

        private RuntimeCompositionFormatter()
        {
        }

        /// <inheritdoc/>
        public RuntimeComposition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            IReadOnlyList<RuntimePart> parts = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimePart>>().Deserialize(ref reader, options);
            int count = reader.ReadInt32();
            ImmutableDictionary<TypeRef, RuntimeExport>.Builder builder = ImmutableDictionary.CreateBuilder<TypeRef, RuntimeComposition.RuntimeExport>();

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();
            IMessagePackFormatter<RuntimeExport> exportFormatter = options.Resolver.GetFormatterWithVerify<RuntimeExport>();

            for (uint i = 0; i < count; i++)
            {
                TypeRef key = typeRefFormatter.Deserialize(ref reader, options);
                RuntimeExport value = exportFormatter.Deserialize(ref reader, options);
                builder.Add(key, value!);
            }

            IReadOnlyDictionary<TypeRef, RuntimeExport> metadataViewsAndProviders = builder.ToImmutable();

            return RuntimeComposition.CreateRuntimeComposition(parts, metadataViewsAndProviders, options.CompositionResolver());
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimeComposition value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimePart>>().Serialize(ref writer, value.Parts, options);
            writer.Write(value.MetadataViewsAndProviders.Count);

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();
            IMessagePackFormatter<RuntimeExport> exportFormatter = options.Resolver.GetFormatterWithVerify<RuntimeExport>();

            foreach (KeyValuePair<TypeRef, RuntimeExport> item in value.MetadataViewsAndProviders)
            {
                typeRefFormatter.Serialize(ref writer, item.Key, options);
                exportFormatter.Serialize(ref writer, item.Value, options);
            }
        }
    }
}
