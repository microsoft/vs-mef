// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;

#pragma warning disable CS3001 // Argument type is not CLS-compliant

    internal class ComposableCatalogFormatter : IMessagePackFormatter<ComposableCatalog>
    {
        public static readonly ComposableCatalogFormatter Instance = new();

        private ComposableCatalogFormatter()
        {
        }

        /// <inheritdoc/>
        public ComposableCatalog Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            IReadOnlyList<ComposablePartDefinition> composablePartDefinition = options.Resolver.GetFormatterWithVerify<IReadOnlyList<ComposablePartDefinition>>().Deserialize(ref reader, options);

            return ComposableCatalog.Create(options.CompositionResolver()).AddParts(composablePartDefinition);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ComposableCatalog value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ComposablePartDefinition>>().Serialize(ref writer, value.Parts, options);
        }
    }
}
