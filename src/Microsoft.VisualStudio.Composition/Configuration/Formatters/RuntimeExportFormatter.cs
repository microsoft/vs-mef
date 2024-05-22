// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

#pragma warning disable CS3001 // Argument type is not CLS-compliant

    internal class RuntimeExportFormatter : IMessagePackFormatter<RuntimeExport?>
    {
        public static readonly RuntimeExportFormatter Instance = new();

        private RuntimeExportFormatter()
        {
        }

        /// <inheritdoc/>
        public RuntimeExport? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out RuntimeExport? value, ref reader))
            {
                string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                MemberRef? member = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
                TypeRef exportedValueType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.DeserializeObject(ref reader, options);

                value = new RuntimeComposition.RuntimeExport(
                    contractName,
                    declaringType,
                    member,
                    exportedValueType,
                    metadata);

                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimeExport? value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value!.ContractName, options);
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringTypeRef, options);
                options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.MemberRef, options);
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ExportedValueTypeRef, options);
                MetadataDictionaryFormatter.SerializeObject(ref writer, value.Metadata, options);
            }
        }
    }
}
