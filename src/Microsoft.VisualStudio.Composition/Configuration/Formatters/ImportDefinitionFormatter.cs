// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Data;
    using MessagePack;
    using MessagePack.Formatters;

#pragma warning disable CS3001 // Argument type is not CLS-compliant

    public class ImportDefinitionFormatter : IMessagePackFormatter<ImportDefinition>
    {
        /// <inheritdoc/>
        public ImportDefinition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            ImportCardinality cardinality = options.Resolver.GetFormatterWithVerify<ImportCardinality>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.DeserializeObject(ref reader, options);
            IReadOnlyList<IImportSatisfiabilityConstraint> constraints = MessagePackCollectionFormatter<IImportSatisfiabilityConstraint>.DeserializeCollection(ref reader, options);
            IReadOnlyList<string> sharingBoundaries = MessagePackCollectionFormatter<string>.DeserializeCollection(ref reader, options);

            return new ImportDefinition(contractName, cardinality, metadata, constraints!, sharingBoundaries!);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ImportDefinition value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
            options.Resolver.GetFormatterWithVerify<ImportCardinality>().Serialize(ref writer, value.Cardinality, options);
            MetadataDictionaryFormatter.SerializeObject(ref writer, value.Metadata, options);
            MessagePackCollectionFormatter<IImportSatisfiabilityConstraint>.SerializeCollection(ref writer, value.ExportConstraints, options);
            MessagePackCollectionFormatter<string>.SerializeCollection(ref writer, value.ExportFactorySharingBoundaries, options);
        }
    }
}
