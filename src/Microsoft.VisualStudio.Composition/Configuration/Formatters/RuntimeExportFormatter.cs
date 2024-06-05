// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeExportFormatter : BaseMessagePackFormatter<RuntimeExport?>
    {
        public static readonly RuntimeExportFormatter Instance = new();

        private RuntimeExportFormatter()
            : base(expectedArrayElementCount: 5)
        {
        }

        /// <inheritdoc/>
        protected override RuntimeExport? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = reader.ReadString()!;
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

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, RuntimeExport? value, MessagePackSerializerOptions options)
        {
            writer.Write(value!.ContractName);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringTypeRef, options);
            options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.MemberRef, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ExportedValueTypeRef, options);
            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
        }
    }
}
