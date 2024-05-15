// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;

    public class ComposableCatalogFormatter : IMessagePackFormatter<ComposableCatalog>
    {
        /// <inheritdoc/>
        public ComposableCatalog Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            //IImmutableSet<ComposablePartDefinition> composablePartDefinition = options.Resolver.GetFormatterWithVerify<IImmutableSet<ComposablePartDefinition>>().Deserialize(ref reader, options);

            var composablePartDefinition = CollectionFormatter<ComposablePartDefinition>.DeserializeCollection(ref reader, options);
            Resolver resolver = options.Resolver.GetFormatterWithVerify<Resolver>().Deserialize(ref reader, options);

            return ComposableCatalog.Create(resolver).AddParts(composablePartDefinition);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ComposableCatalog value, MessagePackSerializerOptions options)
        {
            CollectionFormatter<ComposablePartDefinition>.SerializeCollection(ref writer, value.Parts, options);
            //options.Resolver.GetFormatterWithVerify<IImmutableSet<ComposablePartDefinition>>().Serialize(ref writer, value.Parts, options);
            options.Resolver.GetFormatterWithVerify<Resolver>().Serialize(ref writer, value.Resolver, options);
        }
    }
}
