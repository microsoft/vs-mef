// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;
using static Microsoft.VisualStudio.Composition.RuntimeComposition;

internal class RuntimeExportFormatter : IMessagePackFormatter<RuntimeExport?>
{
    public static readonly RuntimeExportFormatter Instance = new();

    private RuntimeExportFormatter()
    {
    }

    /// <inheritdoc/>
    public RuntimeExport? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
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
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(RuntimeExport)}. Expected: {5}, Actual: {actualCount}");
            }

            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            MemberRef? member = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
            TypeRef exportedValueType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

            return new RuntimeComposition.RuntimeExport(
                contractName,
                declaringType,
                member,
                exportedValueType,
                metadata);
        }
        finally
        {
            reader.Depth--;
        }
    }

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, RuntimeExport? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(5);

        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
        options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringTypeRef, options);
        options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.MemberRef, options);
        options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ExportedValueTypeRef, options);
        MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
    }
}
