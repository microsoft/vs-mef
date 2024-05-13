// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ImportDefinitionBindingFormatter : IMessagePackFormatter<ImportDefinitionBinding>
    {
        /// <inheritdoc/>
        public ImportDefinitionBinding Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            bool isMember = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

            ParameterRef? importingParameterRef = null;
            MemberRef? importingMemberRef = null;

            if (!isMember)
            {
                importingParameterRef = options.Resolver.GetFormatterWithVerify<ParameterRef?>().Deserialize(ref reader, options);
            }
            else
            {
                importingMemberRef = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
            }


            Type composablePartType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            TypeRef composablePartTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            Type? exportFactoryType = options.Resolver.GetFormatterWithVerify<Type?>().Deserialize(ref reader, options);
            ImportDefinition importDefinition = options.Resolver.GetFormatterWithVerify<ImportDefinition>().Deserialize(ref reader, options);
            Type importingSiteType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            TypeRef importingSiteTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            Type importingSiteTypeWithoutCollection = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            Type? importingSiteElementType = options.Resolver.GetFormatterWithVerify<Type?>().Deserialize(ref reader, options);
            TypeRef importingSiteElementTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            bool isExportFactory = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
            bool isLazy = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
            Type? metadataType = options.Resolver.GetFormatterWithVerify<Type?>().Deserialize(ref reader, options);

            return isMember
                ? new ImportDefinitionBinding(importDefinition, composablePartTypeRef, importingMemberRef, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef)
                : new ImportDefinitionBinding(importDefinition, composablePartTypeRef, importingParameterRef, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
        }

        public void Serialize(ref MessagePackWriter writer, ImportDefinitionBinding value, MessagePackSerializerOptions options)
        {
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

            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ComposablePartType, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ComposablePartTypeRef, options);
            options.Resolver.GetFormatterWithVerify<Type?>().Serialize(ref writer, value.ExportFactoryType, options);
            options.Resolver.GetFormatterWithVerify<ImportDefinition>().Serialize(ref writer, value.ImportDefinition, options);
            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ImportingSiteType, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeRef, options);
            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ImportingSiteTypeWithoutCollection, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);
            options.Resolver.GetFormatterWithVerify<Type?>().Serialize(ref writer, value.ImportingSiteElementType, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteElementTypeRef, options);
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsExportFactory, options);
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsLazy, options);
            options.Resolver.GetFormatterWithVerify<Type?>().Serialize(ref writer, value.MetadataType, options);
        }
    }
}
