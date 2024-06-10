// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition;

using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Formatter;

internal class ExportDefinitionFormatter : IMessagePackFormatter<ExportDefinition?>
{
    public static readonly ExportDefinitionFormatter Instance = new();

    private ExportDefinitionFormatter()
    {
    }

    /// <inheritdoc/>
    public ExportDefinition? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
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
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(ExportDefinition)}. Expected: {2}, Actual: {actualCount}");
            }

            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

            return new ExportDefinition(contractName, metadata);
        }
        finally
        {
            reader.Depth--;
        }
    }

    public void Serialize(ref MessagePackWriter writer, ExportDefinition? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(2);

        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
        MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
    }
}
