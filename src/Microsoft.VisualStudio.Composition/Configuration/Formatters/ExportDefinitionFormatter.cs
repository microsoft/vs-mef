// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Formatter;

    internal class ExportDefinitionFormatter : IMessagePackFormatter<ExportDefinition>
    {
        public static readonly ExportDefinitionFormatter Instance = new();

        private ExportDefinitionFormatter()
        {
        }

        /// <inheritdoc/>
        public ExportDefinition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = reader.ReadString()!;
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

            return new ExportDefinition(contractName, metadata);
        }

        public void Serialize(ref MessagePackWriter writer, ExportDefinition value, MessagePackSerializerOptions options)
        {
            writer.Write(value.ContractName);
            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
        }
    }
}
