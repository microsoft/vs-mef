// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ComposablePartDefinitionFormatter : IMessagePackFormatter<ComposablePartDefinition?>
    {
        public static readonly ComposablePartDefinitionFormatter Instance = new();

        private ComposablePartDefinitionFormatter()
        {
        }

        /// <inheritdoc/>
        public ComposablePartDefinition? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 12)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ComposablePartDefinition)}. Expected: {12}, Actual: {actualCount}");
                }

                TypeRef partType = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                IReadOnlyDictionary<string, object?> partMetadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

                IMessagePackFormatter<IReadOnlyList<ExportDefinition>> exportDefinitionFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<ExportDefinition>>();
                IMessagePackFormatter<MemberRef> memberRefFormatter = options.Resolver.GetFormatterWithVerify<MemberRef>();

                IReadOnlyList<ExportDefinition> exportedTypes = exportDefinitionFormatter.Deserialize(ref reader, options);

                IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>> exportingMembers = options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>>().Deserialize(ref reader, options);

                IMessagePackFormatter<IReadOnlyList<ImportDefinitionBinding>> importDefinitionBindingFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<ImportDefinitionBinding>>();
                IReadOnlyList<ImportDefinitionBinding> importingMembers = importDefinitionBindingFormatter.Deserialize(ref reader, options);

                string? sharingBoundary = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);
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
                    exportingMembers,
                    importingMembers,
                    sharingBoundary,
                    onImportsSatisfiedMethods,
                    importingConstructor,
                    importingConstructorImports,
                    creationPolicy,
                    isSharingBoundaryInferred);
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ComposablePartDefinition? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(12);

            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.TypeRef, options);
            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);

            IMessagePackFormatter<IReadOnlyCollection<ExportDefinition>> exportDefinitionFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ExportDefinition>>();
            IMessagePackFormatter<MemberRef> memberRefFormatter = options.Resolver.GetFormatterWithVerify<MemberRef>();

            exportDefinitionFormatter.Serialize(ref writer, value.ExportedTypes, options);

            options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>>().Serialize(ref writer, value.ExportingMembers, options);

            IMessagePackFormatter<IReadOnlyCollection<ImportDefinitionBinding>> importDefinitionBindingFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<ImportDefinitionBinding>>();
            importDefinitionBindingFormatter.Serialize(ref writer, value.ImportingMembers, options);

            options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.SharingBoundary, options);
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
