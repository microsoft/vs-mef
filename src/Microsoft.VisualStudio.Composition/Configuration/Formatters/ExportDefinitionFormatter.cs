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
        /// <inheritdoc/>
        public ExportDefinition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.DeserializeObject(ref reader, options);

            return new ExportDefinition(contractName, metadata);
        }

        public void Serialize(ref MessagePackWriter writer, ExportDefinition value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
            MetadataDictionaryFormatter.SerializeObject(ref writer, value.Metadata, options);
        }
    }
}
