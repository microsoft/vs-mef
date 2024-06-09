// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ExportMetadataValueImportConstraintFormatter : IMessagePackFormatter<ExportMetadataValueImportConstraint?>
    {
        public static readonly ExportMetadataValueImportConstraintFormatter Instance = new();

        private ExportMetadataValueImportConstraintFormatter()
        {
        }

        public ExportMetadataValueImportConstraint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }
            options.Security.DepthStep(ref reader);
            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 2)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ExportMetadataValueImportConstraint)}. Expected: {2}, Actual: {actualCount}");
                }

                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                object? value = options.Resolver.GetFormatterWithVerify<object?>().Deserialize(ref reader, options);
                return new ExportMetadataValueImportConstraint(name, value);
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, ExportMetadataValueImportConstraint? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(2);

            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
            options.Resolver.GetFormatterWithVerify<object?>().Serialize(ref writer, value.Value, options);
        }
    }
}
