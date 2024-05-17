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
            IReadOnlyList<ComposablePartDefinition> composablePartDefinition = CollectionFormatter<ComposablePartDefinition>.DeserializeCollection(ref reader, options);

            return ComposableCatalog.Create(options.CompositionResolver()).AddParts(composablePartDefinition);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ComposableCatalog value, MessagePackSerializerOptions options)
        {
            CollectionFormatter<ComposablePartDefinition>.SerializeCollection(ref writer, value.Parts, options);
        }
    }
}
