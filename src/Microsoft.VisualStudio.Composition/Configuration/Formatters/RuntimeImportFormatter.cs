// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeImportFormatter : BaseMessagePackFormatter<RuntimeImport>
    {
        public static readonly RuntimeImportFormatter Instance = new();

        private RuntimeImportFormatter()
        {
        }

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
        protected override RuntimeImport DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            this.CheckArrayHeaderCount(ref reader, 7);
            var flags = (RuntimeImportFlags)reader.ReadByte();
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

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            TypeRef importingSiteTypeRef = typeRefFormatter.Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef =
    cardinality == ImportCardinality.ZeroOrMore ? typeRefFormatter.Deserialize(ref reader, options) : importingSiteTypeRef;

            IReadOnlyList<RuntimeExport> satisfyingExports = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimeExport>>().Deserialize(ref reader, options);
            IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);
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
        protected override void SerializeData(ref MessagePackWriter writer, RuntimeImport value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(7);

            RuntimeImportFlags flags = RuntimeImportFlags.None;
            flags |= value.ImportingMemberRef == null ? RuntimeImportFlags.IsParameter : 0;
            flags |= value.IsNonSharedInstanceRequired ? RuntimeImportFlags.IsNonSharedInstanceRequired : 0;
            flags |= value.IsExportFactory ? RuntimeImportFlags.IsExportFactory : 0;
            flags |=
                value.Cardinality == ImportCardinality.ExactlyOne ? RuntimeImportFlags.CardinalityExactlyOne :
                value.Cardinality == ImportCardinality.OneOrZero ? RuntimeImportFlags.CardinalityOneOrZero : 0;

            writer.Write((byte)flags);

            if (value.ImportingMemberRef is null)
            {
                options.Resolver.GetFormatterWithVerify<ParameterRef?>().Serialize(ref writer, value.ImportingParameterRef, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.ImportingMemberRef, options);
            }

            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            typeRefFormatter.Serialize(ref writer, value.ImportingSiteTypeRef, options);

            if (value.Cardinality == ImportCardinality.ZeroOrMore)
            {
                typeRefFormatter.Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);
            }
            else
            {
                if (value.ImportingSiteTypeWithoutCollectionRef != value.ImportingSiteTypeRef)
                {
                    throw new ArgumentException($"{nameof(value.ImportingSiteTypeWithoutCollectionRef)} and {nameof(value.ImportingSiteTypeRef)} must be equal when {nameof(value.Cardinality)} is not {nameof(ImportCardinality.ZeroOrMore)}.", nameof(value));
                }
            }

            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeExport>>().Serialize(ref writer, value.SatisfyingExports, options);

            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
            if (value.IsExportFactory)
            {
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Serialize(ref writer, value.ExportFactorySharingBoundaries, options);
            }
        }
    }
}
