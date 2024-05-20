// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

#pragma warning disable CS3001 // Argument type is not CLS-compliant

    public class ImportDefinitionBindingFormatter : IMessagePackFormatter<ImportDefinitionBinding>
    {
        /// <inheritdoc/>
        public ImportDefinitionBinding Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            ImportDefinition importDefinition = options.Resolver.GetFormatterWithVerify<ImportDefinition>().Deserialize(ref reader, options);
            TypeRef part = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            TypeRef importingSiteTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

            MemberRef? member;
            ParameterRef? parameter;
            bool isMember = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
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
        public void Serialize(ref MessagePackWriter writer, ImportDefinitionBinding value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<ImportDefinition>().Serialize(ref writer, value.ImportDefinition, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ComposablePartTypeRef, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeRef, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);

            if (value.ImportingMemberRef is null)
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, false, options);
                options.Resolver.GetFormatterWithVerify<ParameterRef?>().Serialize(ref writer, value.ImportingParameterRef, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, true, options);
                options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.ImportingMemberRef, options);
            }
        }
    }
}
