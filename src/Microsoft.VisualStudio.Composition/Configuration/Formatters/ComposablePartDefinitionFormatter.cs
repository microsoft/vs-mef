// Copyright (c) Microsoft Corporation. All rights reserved.
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
            TypeRef partType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> partMetadata = MetadataDictionaryFormatter.DeserializeObject(ref reader, options); // metadata
            IReadOnlyList<ExportDefinition> exportedTypes = MessagePackCollectionFormatter<ExportDefinition>.DeserializeCollection(ref reader, options);
            ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Builder exportingMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();
            int exportedMembersCount = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

            for (int i = 0; i < exportedMembersCount; i++)
            {
                MemberRef member = options.Resolver.GetFormatterWithVerify<MemberRef>().Deserialize(ref reader, options);
                IReadOnlyList<ExportDefinition> exports = MessagePackCollectionFormatter<ExportDefinition>.DeserializeCollection(ref reader, options);

                exportingMembers.Add(member, exports);
            }

            IReadOnlyList<ImportDefinitionBinding> importingMembers = MessagePackCollectionFormatter<ImportDefinitionBinding>.DeserializeCollection(ref reader, options);
            string? sharingBoundary = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);
            IReadOnlyList<MethodRef> onImportsSatisfiedMethods = MessagePackCollectionFormatter<MethodRef>.DeserializeCollection(ref reader, options);

            var importingConstructor = default(MethodRef);
            IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports = null;

            if (options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options))
            {
                importingConstructor = options.Resolver.GetFormatterWithVerify<MethodRef?>().Deserialize(ref reader, options);
                importingConstructorImports = MessagePackCollectionFormatter<ImportDefinitionBinding?>.DeserializeCollection(ref reader, options)!;
            }

            CreationPolicy creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
            bool isSharingBoundaryInferred = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

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
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ComposablePartDefinition value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.TypeRef, options);
            MetadataDictionaryFormatter.SerializeObject(ref writer, value.Metadata, options);
            MessagePackCollectionFormatter<ExportDefinition>.SerializeCollection(ref writer, value.ExportedTypes, options);
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.ExportingMembers.Count(), options);
            foreach (KeyValuePair<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMember in value.ExportingMembers)
            {
                options.Resolver.GetFormatterWithVerify<MemberRef>().Serialize(ref writer, exportingMember.Key, options);
                MessagePackCollectionFormatter<ExportDefinition>.SerializeCollection(ref writer, exportingMember.Value, options);
            }

            MessagePackCollectionFormatter<ImportDefinitionBinding>.SerializeCollection(ref writer, value.ImportingMembers, options);
            options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.SharingBoundary, options);
            MessagePackCollectionFormatter<MethodRef>.SerializeCollection(ref writer, value.OnImportsSatisfiedMethodRefs, options);

            if (value.ImportingConstructorOrFactoryRef is null)
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, false, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, true, options);
                options.Resolver.GetFormatterWithVerify<MethodRef?>().Serialize(ref writer, value.ImportingConstructorOrFactoryRef, options);
                MessagePackCollectionFormatter<ImportDefinitionBinding?>.SerializeCollection(ref writer, value.ImportingConstructorImports!, options);
            }

            options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, value.CreationPolicy, options);
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsSharingBoundaryInferred, options);
        }
    }
}
