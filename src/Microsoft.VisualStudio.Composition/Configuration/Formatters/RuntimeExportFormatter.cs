// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeExportFormatter : IMessagePackFormatter<RuntimeExport>
    {
        /// <inheritdoc/>
        public RuntimeExport Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            TypeRef declaringType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            MemberRef? member = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
            TypeRef exportedValueType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = ObjectFormatter.DeserializeObject(ref reader, options);  //  options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<string, object?>>().Deserialize(ref reader, options);

            var value = new RuntimeComposition.RuntimeExport(
                contractName,
                declaringType,
                member,
                exportedValueType,
                metadata);

            //this.OnDeserializedReusableObject(id, value); ankit to check about this.

            return value;
        }

        public void Serialize(ref MessagePackWriter writer, RuntimeExport value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringTypeRef, options);
            options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.MemberRef, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ExportedValueTypeRef, options);
            ObjectFormatter.SerializeObject(ref writer, value.Metadata, options);
        }
    }
}
