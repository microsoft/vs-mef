﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimeImportFormatter : IMessagePackFormatter<RuntimeImport>
    {
        /// <inheritdoc/>
        public RuntimeImport Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            RuntimeImportFlags flags = (RuntimeImportFlags)options.Resolver.GetFormatterWithVerify<byte>().Deserialize(ref reader, options);
            var cardinality =
              (flags & RuntimeImportFlags.CardinalityOneOrZero) == RuntimeImportFlags.CardinalityOneOrZero ? ImportCardinality.OneOrZero :
              (flags & RuntimeImportFlags.CardinalityExactlyOne) == RuntimeImportFlags.CardinalityExactlyOne ? ImportCardinality.ExactlyOne :
              ImportCardinality.ZeroOrMore;
            bool isExportFactory = (flags & RuntimeImportFlags.IsExportFactory) == RuntimeImportFlags.IsExportFactory;

            MemberRef? importingMember = default(MemberRef);
            ParameterRef? importingParameter = default(ParameterRef);
            if ((flags & RuntimeImportFlags.IsParameter) == RuntimeImportFlags.IsParameter)
            {
                importingParameter = options.Resolver.GetFormatterWithVerify<ParameterRef?>().Deserialize(ref reader, options);
            }
            else
            {
                importingMember = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
            }

            var importingSiteTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef =
    cardinality == ImportCardinality.ZeroOrMore ? options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options) : importingSiteTypeRef;

            var satisfyingExports = CollectionFormatter<RuntimeExport>.DeserializeCollection(ref reader, options);
            //var satisfyingExports = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeExport>>().Deserialize(ref reader, options);
            var metadata = ObjectFormatter.DeserializeObject(ref reader, options);
            //options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<string, object?>>().Deserialize(ref reader, options);

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

            CollectionFormatter<RuntimeExport>.SerializeCollection(ref writer, value.SatisfyingExports, options);
            //options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeExport>>().Serialize(ref writer, value.SatisfyingExports, options);

            ObjectFormatter.SerializeObject(ref writer, value.Metadata, options);
            //options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<string, object?>>().Serialize(ref writer, value.Metadata, options); //todo ankitall object formamter

            if (value.IsExportFactory)
            {
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<string>>().Serialize(ref writer, value.ExportFactorySharingBoundaries, options);
            }
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
    }
}
