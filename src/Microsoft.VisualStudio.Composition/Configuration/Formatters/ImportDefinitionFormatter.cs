// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using System.Data;
    using MessagePack;
    using MessagePack.Formatters;

    internal class ImportDefinitionFormatter : IMessagePackFormatter<ImportDefinition>
    {
        /// <inheritdoc/>
        public ImportDefinition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            ImportCardinality cardinality = options.Resolver.GetFormatterWithVerify<ImportCardinality>().Deserialize(ref reader, options);
            var metadata = ObjectFormatter.DeserializeObject(ref reader, options);
            var constraints = CollectionFormatter<IImportSatisfiabilityConstraint>.DeserializeCollection(ref reader, options);
            var sharingBoundaries = CollectionFormatter<string>.DeserializeCollection(ref reader, options);

            return new ImportDefinition(contractName, cardinality, metadata, constraints!, sharingBoundaries!);
        }

        public void Serialize(ref MessagePackWriter writer, ImportDefinition value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
            options.Resolver.GetFormatterWithVerify<ImportCardinality>().Serialize(ref writer, value.Cardinality, options);
            ObjectFormatter.SerializeObject(ref writer, value.Metadata, options);
            CollectionFormatter<IImportSatisfiabilityConstraint>.SerializeCollection(ref writer, value.ExportConstraints, options);
            CollectionFormatter<string>.SerializeCollection(ref writer, value.ExportFactorySharingBoundaries, options);
        }
    }
}
