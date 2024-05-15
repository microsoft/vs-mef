﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ComposablePartDefinitionFormatter : IMessagePackFormatter<ComposablePartDefinition>
    {
        /// <inheritdoc/>
        public ComposablePartDefinition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {

            var partType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            var partMetadata = ObjectFormatter.DeserializeObject(ref reader, options); // metadata
            var exportedTypes = CollectionFormatter<ExportDefinition>.DeserializeCollection(ref reader, options);
            var exportingMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();
            int exportedMembersCount = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

            for (int i = 0; i < exportedMembersCount; i++)
            {
                var member = options.Resolver.GetFormatterWithVerify<MemberRef>().Deserialize(ref reader, options);
                var exports = CollectionFormatter<ExportDefinition>.DeserializeCollection(ref reader, options);

                exportingMembers.Add(member, exports);
            }

            var importingMembers = CollectionFormatter<ImportDefinitionBinding>.DeserializeCollection(ref reader, options);
            var sharingBoundary = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);
            IReadOnlyList<MethodRef> onImportsSatisfiedMethods = CollectionFormatter<MethodRef>.DeserializeCollection(ref reader, options);

            MethodRef? importingConstructor = default(MethodRef);
            IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports = null;

            if (options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options))
            {
                importingConstructor = options.Resolver.GetFormatterWithVerify<MethodRef?>().Deserialize(ref reader, options);
                importingConstructorImports = CollectionFormatter<ImportDefinitionBinding?>.DeserializeCollection(ref reader, options); 
            }

            var creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
            var isSharingBoundaryInferred = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

            return new ComposablePartDefinition(
                partType,
                partMetadata,
                exportedTypes,
                exportingMembers.ToImmutable(),
                importingMembers,
                sharingBoundary,
                onImportsSatisfiedMethods,
                importingConstructor,
                importingConstructorImports,
                creationPolicy,
                isSharingBoundaryInferred);

            //string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            //options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.DeclaringTypeRef, options);
            //throw new NotImplementedException();
        }

        public void Serialize(ref MessagePackWriter writer, ComposablePartDefinition value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.TypeRef, options);
            ObjectFormatter.SerializeObject(ref writer, value.Metadata, options);
            CollectionFormatter<ExportDefinition>.SerializeCollection(ref writer, value.ExportedTypes, options);
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.ExportingMembers.Count(), options);
            foreach (var exportingMember in value.ExportingMembers)
            {
                options.Resolver.GetFormatterWithVerify<MemberRef>().Serialize(ref writer, exportingMember.Key, options);
                CollectionFormatter<ExportDefinition>.SerializeCollection(ref writer, exportingMember.Value, options);
            }

            CollectionFormatter<ImportDefinitionBinding>.SerializeCollection(ref writer, value.ImportingMembers, options);
            options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.SharingBoundary, options);
            CollectionFormatter<MethodRef>.SerializeCollection(ref writer, value.OnImportsSatisfiedMethodRefs, options);

            if (value.ImportingConstructorOrFactoryRef is null)
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, false, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, true, options);
                options.Resolver.GetFormatterWithVerify<MethodRef?>().Serialize(ref writer, value.ImportingConstructorOrFactoryRef, options);
                CollectionFormatter<ImportDefinitionBinding?>.SerializeCollection(ref writer, value.ImportingConstructorImports, options);
            }

            options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, value.CreationPolicy, options);
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsSharingBoundaryInferred, options);
        }
    }
}
