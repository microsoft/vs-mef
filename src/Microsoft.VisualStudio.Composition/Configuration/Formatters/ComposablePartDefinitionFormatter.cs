// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ComposablePartDefinitionFormatter : IMessagePackFormatter<ComposablePartDefinition>
    {
        public static readonly ComposablePartDefinitionFormatter Instance = new();

        private ComposablePartDefinitionFormatter()
        {
        }

        /// <inheritdoc/>
        public ComposablePartDefinition Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            TypeRef partType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> partMetadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

            IMessagePackFormatter<IReadOnlyList<ExportDefinition>> exportDefinitionFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<ExportDefinition>>();
            IMessagePackFormatter<MemberRef> memberRefFormatter = options.Resolver.GetFormatterWithVerify<MemberRef>();

            IReadOnlyList<ExportDefinition> exportedTypes = exportDefinitionFormatter.Deserialize(ref reader, options);

            ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Builder exportingMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();
            int exportedMembersCount = reader.ReadInt32();

            for (int i = 0; i < exportedMembersCount; i++)
            {
                MemberRef member = memberRefFormatter.Deserialize(ref reader, options);
                IReadOnlyList<ExportDefinition> exports = exportDefinitionFormatter.Deserialize(ref reader, options);

                exportingMembers.Add(member, exports);
            }

            IMessagePackFormatter<IReadOnlyList<ImportDefinitionBinding>> importDefinitionBindingFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<ImportDefinitionBinding>>();
            IReadOnlyList<ImportDefinitionBinding> importingMembers = importDefinitionBindingFormatter.Deserialize(ref reader, options);

            string? sharingBoundary = reader.ReadString();
            IReadOnlyList<MethodRef> onImportsSatisfiedMethods = options.Resolver.GetFormatterWithVerify<IReadOnlyList<MethodRef>>().Deserialize(ref reader, options);

            var importingConstructor = default(MethodRef);
            IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports = null;

            if (reader.ReadBoolean())
            {
                importingConstructor = options.Resolver.GetFormatterWithVerify<MethodRef?>().Deserialize(ref reader, options);
                importingConstructorImports = importDefinitionBindingFormatter.Deserialize(ref reader, options);
            }

            CreationPolicy creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
            bool isSharingBoundaryInferred = reader.ReadBoolean();

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
            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);

            IMessagePackFormatter<IReadOnlyCollection<ExportDefinition>> exportDefinitionFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ExportDefinition>>();
            IMessagePackFormatter<MemberRef> memberRefFormatter = options.Resolver.GetFormatterWithVerify<MemberRef>();

            exportDefinitionFormatter.Serialize(ref writer, value.ExportedTypes, options);
            writer.Write(value.ExportingMembers.Count);

            foreach (KeyValuePair<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMember in value.ExportingMembers)
            {
                memberRefFormatter.Serialize(ref writer, exportingMember.Key, options);
                exportDefinitionFormatter.Serialize(ref writer, exportingMember.Value, options);
            }

            IMessagePackFormatter<IReadOnlyCollection<ImportDefinitionBinding>> importDefinitionBindingFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ImportDefinitionBinding>>();
            importDefinitionBindingFormatter.Serialize(ref writer, value.ImportingMembers, options);

            writer.Write(value.SharingBoundary);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<MethodRef>>().Serialize(ref writer, value.OnImportsSatisfiedMethodRefs, options);

            if (value.ImportingConstructorOrFactoryRef is null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                options.Resolver.GetFormatterWithVerify<MethodRef?>().Serialize(ref writer, value.ImportingConstructorOrFactoryRef, options);
                importDefinitionBindingFormatter.Serialize(ref writer, value.ImportingConstructorImports!, options);
            }

            options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, value.CreationPolicy, options);
            writer.Write(value.IsSharingBoundaryInferred);
        }
    }
}
