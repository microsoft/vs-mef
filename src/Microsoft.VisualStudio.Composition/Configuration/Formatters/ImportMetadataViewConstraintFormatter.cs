// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ImportMetadataViewConstraintFormatter : IMessagePackFormatter<ImportMetadataViewConstraint?>
    {
        public static readonly ImportMetadataViewConstraintFormatter Instance = new();

        private ImportMetadataViewConstraintFormatter()
        {
        }

        public ImportMetadataViewConstraint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
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
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ImportMetadataViewConstraint)}. Expected: {1}, Actual: {actualCount}");
                }

                ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement> requirements = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>>().Deserialize(ref reader, options);
                return new ImportMetadataViewConstraint(requirements, options.CompositionResolver());
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, ImportMetadataViewConstraint? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);

            options.Resolver.GetFormatterWithVerify<ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>>().Serialize(ref writer, value.Requirements, options);
        }
    }
}
