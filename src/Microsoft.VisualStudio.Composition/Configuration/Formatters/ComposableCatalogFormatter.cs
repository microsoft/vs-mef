// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;

    internal class ComposableCatalogFormatter : BaseMessagePackFormatter<ComposableCatalog>
    {
        public static readonly ComposableCatalogFormatter Instance = new();

        private ComposableCatalogFormatter()
            : base(arrayElementCount: 1)
        {
        }

        /// <inheritdoc/>
        protected override ComposableCatalog DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            IReadOnlyList<ComposablePartDefinition> composablePartDefinition = options.Resolver.GetFormatterWithVerify<IReadOnlyList<ComposablePartDefinition>>().Deserialize(ref reader, options);
            return ComposableCatalog.Create(options.CompositionResolver()).AddParts(composablePartDefinition);
        }

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, ComposableCatalog value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ComposablePartDefinition>>().Serialize(ref writer, value.Parts, options);
        }
    }
}
