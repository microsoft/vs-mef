// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Data;
    using MessagePack;
    using MessagePack.Formatters;

    internal class ImportDefinitionFormatter : BaseMessagePackFormatter<ImportDefinition>
    {
        public static readonly ImportDefinitionFormatter Instance = new();

        private ImportDefinitionFormatter()
            : base(arrayElementCount: 5)
        {
        }

        /// <inheritdoc/>
        protected override ImportDefinition DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = reader.ReadString()!;
            ImportCardinality cardinality = options.Resolver.GetFormatterWithVerify<ImportCardinality>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

            IReadOnlyList<IImportSatisfiabilityConstraint> constraints = options.Resolver.GetFormatterWithVerify<IReadOnlyList<IImportSatisfiabilityConstraint>>().Deserialize(ref reader, options);
            IReadOnlyList<string> sharingBoundaries = options.Resolver.GetFormatterWithVerify<IReadOnlyList<string>>().Deserialize(ref reader, options);

            return new ImportDefinition(contractName, cardinality, metadata, constraints!, sharingBoundaries!);
        }

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, ImportDefinition value, MessagePackSerializerOptions options)
        {
            writer.Write(value.ContractName);
            options.Resolver.GetFormatterWithVerify<ImportCardinality>().Serialize(ref writer, value.Cardinality, options);
            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<IImportSatisfiabilityConstraint>>().Serialize(ref writer, value.ExportConstraints, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Serialize(ref writer, value.ExportFactorySharingBoundaries, options);
        }
    }
}
