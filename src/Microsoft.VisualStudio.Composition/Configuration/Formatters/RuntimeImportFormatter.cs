// Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT license. See
// LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeImportFormatter : IMessagePackFormatter<RuntimeImport>
    {
        public enum RuntimeImportFlags : byte
        {
            None = 0x00,
            IsNonSharedInstanceRequired = 0x01,
            IsExportFactory = 0x02,
            CardinalityExactlyOne = 0x04,
            CardinalityOneOrZero = 0x08,
            IsParameter = 0x10,
        }

        /// <inheritdoc/>
        public RuntimeImport Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var flags = (RuntimeImportFlags)options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref reader, options);
            ImportCardinality cardinality =
              (flags & RuntimeImportFlags.CardinalityOneOrZero) == RuntimeImportFlags.CardinalityOneOrZero ? ImportCardinality.OneOrZero :
              (flags & RuntimeImportFlags.CardinalityExactlyOne) == RuntimeImportFlags.CardinalityExactlyOne ? ImportCardinality.ExactlyOne :
              ImportCardinality.ZeroOrMore;
            bool isExportFactory = (flags & RuntimeImportFlags.IsExportFactory) == RuntimeImportFlags.IsExportFactory;

            var importingMember = default(MemberRef);
            var importingParameter = default(ParameterRef);
            if ((flags & RuntimeImportFlags.IsParameter) == RuntimeImportFlags.IsParameter)
            {
                importingParameter = options.Resolver.GetFormatterWithVerify<ParameterRef?>().Deserialize(ref reader, options);
            }
            else
            {
                importingMember = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
            }

            TypeRef importingSiteTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef =
    cardinality == ImportCardinality.ZeroOrMore ? options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options) : importingSiteTypeRef;

            IReadOnlyList<RuntimeExport> satisfyingExports = MessagePackCollectionFormatter<RuntimeExport>.DeserializeCollection(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.DeserializeObject(ref reader, options);
            IReadOnlyCollection<string?> exportFactorySharingBoundaries = isExportFactory
                ? options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Deserialize(ref reader, options)
                : ImmutableList<string>.Empty;

            return importingMember == null
                        ? new RuntimeComposition.RuntimeImport(
                            importingParameterRef: importingParameter!,
                            importingSiteTypeRef,
                            importingSiteTypeWithoutCollectionRef,
                            cardinality,
                            satisfyingExports: satisfyingExports.ToList()!,
                            (flags & RuntimeImportFlags.IsNonSharedInstanceRequired) == RuntimeImportFlags.IsNonSharedInstanceRequired,
                            isExportFactory,
                            metadata,
                            exportFactorySharingBoundaries!)
                        : new RuntimeComposition.RuntimeImport(
                            importingMember,
                            importingSiteTypeRef,
                            importingSiteTypeWithoutCollectionRef,
                            cardinality,
                            satisfyingExports.ToList()!,
                            (flags & RuntimeImportFlags.IsNonSharedInstanceRequired) == RuntimeImportFlags.IsNonSharedInstanceRequired,
                            isExportFactory,
                            metadata,
                            exportFactorySharingBoundaries!);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimeImport value, MessagePackSerializerOptions options)
        {
            RuntimeImportFlags flags = RuntimeImportFlags.None;
            flags |= value.ImportingMemberRef == null ? RuntimeImportFlags.IsParameter : 0;
            flags |= value.IsNonSharedInstanceRequired ? RuntimeImportFlags.IsNonSharedInstanceRequired : 0;
            flags |= value.IsExportFactory ? RuntimeImportFlags.IsExportFactory : 0;
            flags |=
                value.Cardinality == ImportCardinality.ExactlyOne ? RuntimeImportFlags.CardinalityExactlyOne :
                value.Cardinality == ImportCardinality.OneOrZero ? RuntimeImportFlags.CardinalityOneOrZero : 0;

            options.Resolver.GetFormatterWithVerify<byte>().Serialize(ref writer, (byte)flags, options);

            if (value.ImportingMemberRef is null)
            {
                options.Resolver.GetFormatterWithVerify<ParameterRef?>().Serialize(ref writer, value.ImportingParameterRef, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.ImportingMemberRef, options);
            }

            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeRef, options);

            if (value.Cardinality == ImportCardinality.ZeroOrMore)
            {
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);
            }
            else
            {
                if (value.ImportingSiteTypeWithoutCollectionRef != value.ImportingSiteTypeRef)
                {
                    throw new ArgumentException($"{nameof(value.ImportingSiteTypeWithoutCollectionRef)} and {nameof(value.ImportingSiteTypeRef)} must be equal when {nameof(value.Cardinality)} is not {nameof(ImportCardinality.ZeroOrMore)}.", nameof(value));
                }
            }

            MessagePackCollectionFormatter<RuntimeExport>.SerializeCollection(ref writer, value.SatisfyingExports, options);
            MetadataDictionaryFormatter.SerializeObject(ref writer, value.Metadata, options);
            if (value.IsExportFactory)
            {
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Serialize(ref writer, value.ExportFactorySharingBoundaries, options);
            }
        }
    }
}
