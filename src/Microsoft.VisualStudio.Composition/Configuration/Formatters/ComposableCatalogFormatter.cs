// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using MessagePack;
    using MessagePack.Formatters;

    internal class ComposableCatalogFormatter : IMessagePackFormatter<ComposableCatalog?>
    {
        public static readonly ComposableCatalogFormatter Instance = new();

        private ComposableCatalogFormatter()
        {
        }

        /// <inheritdoc/>
        public ComposableCatalog? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);

            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 1)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ComposableCatalog)}. Expected: {1}, Actual: {actualCount}");
                }

                IReadOnlyCollection<ComposablePartDefinition> composablePartDefinition = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ComposablePartDefinition>>().Deserialize(ref reader, options);
                return ComposableCatalog.Create(options.CompositionResolver()).AddParts(composablePartDefinition);
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ComposableCatalog? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);

            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ComposablePartDefinition>>().Serialize(ref writer, value.Parts, options);
        }
    }
}
