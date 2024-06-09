// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeCompositionFormatter : IMessagePackFormatter<RuntimeComposition?>
    {
        public static readonly RuntimeCompositionFormatter Instance = new();

        private RuntimeCompositionFormatter()
        {
        }

        /// <inheritdoc/>
        public RuntimeComposition? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }
            try
            {
                options.Security.DepthStep(ref reader);
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 2)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(RuntimeComposition)}. Expected: {2}, Actual: {actualCount}");
                }

                IReadOnlyCollection<RuntimePart> parts = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimePart>>().Deserialize(ref reader, options);

                IReadOnlyDictionary<TypeRef, RuntimeExport> metadataViewsAndProviders = options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<TypeRef, RuntimeExport>>().Deserialize(ref reader, options);

                return RuntimeComposition.CreateRuntimeComposition(parts, metadataViewsAndProviders, options.CompositionResolver());
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimeComposition? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(2);

            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimePart>>().Serialize(ref writer, value.Parts, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<TypeRef, RuntimeExport>>().Serialize(ref writer, value.MetadataViewsAndProviders, options);
        }
    }
}
