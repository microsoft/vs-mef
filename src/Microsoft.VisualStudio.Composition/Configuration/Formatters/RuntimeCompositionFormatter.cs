// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeCompositionFormatter : IMessagePackFormatter<RuntimeComposition>
    {
        /// <inheritdoc/>
        public RuntimeComposition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            IReadOnlyList<RuntimePart> parts = MessagePackCollectionFormatter<RuntimePart>.DeserializeCollection(ref reader, options);
            int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
            ImmutableDictionary<TypeRef, RuntimeExport>.Builder builder = ImmutableDictionary.CreateBuilder<TypeRef, RuntimeComposition.RuntimeExport>();

            for (uint i = 0; i < count; i++)
            {
                TypeRef key = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                RuntimeExport value = options.Resolver.GetFormatterWithVerify<RuntimeExport>().Deserialize(ref reader, options);
                builder.Add(key, value!);
            }

            IReadOnlyDictionary<TypeRef, RuntimeExport> metadataViewsAndProviders = builder.ToImmutable();

            return RuntimeComposition.CreateRuntimeComposition(parts, metadataViewsAndProviders, options.CompositionResolver());
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimeComposition value, MessagePackSerializerOptions options)
        {
            MessagePackCollectionFormatter<RuntimePart>.SerializeCollection(ref writer, value.Parts, options);

            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataViewsAndProviders.Count(), options);
            foreach (KeyValuePair<TypeRef, RuntimeExport> item in value.MetadataViewsAndProviders)
            {
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, item.Key, options);
                options.Resolver.GetFormatterWithVerify<RuntimeExport>().Serialize(ref writer, item.Value, options);
            }
        }
    }
}
