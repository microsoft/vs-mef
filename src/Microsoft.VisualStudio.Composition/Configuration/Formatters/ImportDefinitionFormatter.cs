// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using MessagePack;
using MessagePack.Formatters;

internal class ImportDefinitionFormatter : IMessagePackFormatter<ImportDefinition?>
{
    public static readonly ImportDefinitionFormatter Instance = new();

    private ImportDefinitionFormatter()
    {
    }

    /// <inheritdoc/>
    public ImportDefinition? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        options.Security.DepthStep(ref reader);

        try
        {
            var actualCount = reader.ReadArrayHeader();
            if (actualCount != 5)
            {
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(ImportDefinition)}. Expected: {5}, Actual: {actualCount}");
            }

            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            ImportCardinality cardinality = options.Resolver.GetFormatterWithVerify<ImportCardinality>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

            IReadOnlyList<IImportSatisfiabilityConstraint> constraints = options.Resolver.GetFormatterWithVerify<IReadOnlyList<IImportSatisfiabilityConstraint>>().Deserialize(ref reader, options);
            IReadOnlyCollection<string> sharingBoundaries = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Deserialize(ref reader, options);

            return new ImportDefinition(contractName, cardinality, metadata, constraints, sharingBoundaries);
        }
        finally
        {
            reader.Depth--;
        }
    }

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, ImportDefinition? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(5);

        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
        options.Resolver.GetFormatterWithVerify<ImportCardinality>().Serialize(ref writer, value.Cardinality, options);
        MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
        options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<IImportSatisfiabilityConstraint>>().Serialize(ref writer, value.ExportConstraints, options);
        options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Serialize(ref writer, value.ExportFactorySharingBoundaries, options);
    }
}
