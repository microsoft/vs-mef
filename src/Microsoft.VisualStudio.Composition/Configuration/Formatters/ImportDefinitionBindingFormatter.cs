// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ImportDefinitionBindingFormatter : BaseMessagePackFormatter<ImportDefinitionBinding>
    {
        public static readonly ImportDefinitionBindingFormatter Instance = new();

        private ImportDefinitionBindingFormatter()
        {
        }

        /// <inheritdoc/>
        protected override ImportDefinitionBinding DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            this.CheckArrayHeaderCount(ref reader, 6);
            ImportDefinition importDefinition = options.Resolver.GetFormatterWithVerify<ImportDefinition>().Deserialize(ref reader, options);
            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            TypeRef part = typeRefFormatter.Deserialize(ref reader, options);
            TypeRef importingSiteTypeRef = typeRefFormatter.Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef = typeRefFormatter.Deserialize(ref reader, options);

            MemberRef? member;
            ParameterRef? parameter;
            bool isMember = reader.ReadBoolean();
            if (isMember)
            {
                member = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options)!;
                return new ImportDefinitionBinding(importDefinition, part, member, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
            }
            else
            {
                parameter = options.Resolver.GetFormatterWithVerify<ParameterRef?>().Deserialize(ref reader, options)!;
                return new ImportDefinitionBinding(importDefinition, part, parameter, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
            }
        }

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, ImportDefinitionBinding value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(6);
            options.Resolver.GetFormatterWithVerify<ImportDefinition>().Serialize(ref writer, value.ImportDefinition, options);
            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            typeRefFormatter.Serialize(ref writer, value.ComposablePartTypeRef, options);
            typeRefFormatter.Serialize(ref writer, value.ImportingSiteTypeRef, options);
            typeRefFormatter.Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);

            if (value.ImportingMemberRef is null)
            {
                writer.Write(false);
                options.Resolver.GetFormatterWithVerify<ParameterRef?>().Serialize(ref writer, value.ImportingParameterRef, options);
            }
            else
            {
                writer.Write(true);
                options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.ImportingMemberRef, options);
            }
        }
    }
}
