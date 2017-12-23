// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class CachedComposition : ICompositionCacheManager, IRuntimeCompositionCacheManager
    {
        private static readonly Encoding TextEncoding = Encoding.UTF8;

        public Task SaveAsync(CompositionConfiguration configuration, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanWrite, "cacheStream", Strings.WritableStreamRequired);

            return Task.Run(async delegate
            {
                var compositionRuntime = RuntimeComposition.CreateRuntimeComposition(configuration);

                await this.SaveAsync(compositionRuntime, cacheStream, cancellationToken);
            });
        }

        public Task SaveAsync(RuntimeComposition composition, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(composition, nameof(composition));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanWrite, "cacheStream", Strings.WritableStreamRequired);

            return Task.Run(() =>
            {
                using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(writer, composition.Parts.Count * 5, composition.Resolver);
                    context.Write(composition);
                    context.FinalizeObjectTableCapacity();
                }
            });
        }

        public Task<RuntimeComposition> LoadRuntimeCompositionAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanRead, "cacheStream", Strings.ReadableStreamRequired);
            Requires.NotNull(resolver, nameof(resolver));

            return Task.Run(() =>
            {
                using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(reader, resolver);
                    var runtimeComposition = context.ReadRuntimeComposition();
                    return runtimeComposition;
                }
            });
        }

        public async Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            var runtimeComposition = await this.LoadRuntimeCompositionAsync(cacheStream, resolver, cancellationToken);
            return runtimeComposition.CreateExportProviderFactory();
        }

        private class SerializationContext : SerializationContextBase
        {
            internal SerializationContext(BinaryReader reader, Resolver resolver)
                : base(reader, resolver)
            {
            }

            internal SerializationContext(BinaryWriter writer, int estimatedObjectCount, Resolver resolver)
                : base(writer, estimatedObjectCount, resolver)
            {
            }

            private enum RuntimeImportFlags : byte
            {
                None = 0x00,
                IsNonSharedInstanceRequired = 0x01,
                IsExportFactory = 0x02,
                CardinalityExactlyOne = 0x04,
                CardinalityOneOrZero = 0x08,
                IsParameter = 0x10,
            }

            internal void Write(RuntimeComposition compositionRuntime)
            {
                Requires.NotNull(this.writer, "writer");
                Requires.NotNull(compositionRuntime, nameof(compositionRuntime));

                using (this.Trace("RuntimeComposition"))
                {
                    this.Write(compositionRuntime.Parts, this.Write);
                    this.Write(compositionRuntime.MetadataViewsAndProviders);
                }

                this.TraceStats();
            }

            internal RuntimeComposition ReadRuntimeComposition()
            {
                Requires.NotNull(this.reader, "reader");

                RuntimeComposition result;
                using (this.Trace("RuntimeComposition"))
                {
                    var parts = this.ReadList(this.reader, this.ReadRuntimePart);
                    var metadataViewsAndProviders = this.ReadMetadataViewsAndProviders();

                    result = RuntimeComposition.CreateRuntimeComposition(parts, metadataViewsAndProviders, this.Resolver);
                }

                this.TraceStats();
                return result;
            }

            private void Write(RuntimeComposition.RuntimeExport export)
            {
                using (this.Trace("RuntimeExport"))
                {
                    if (this.TryPrepareSerializeReusableObject(export))
                    {
                        this.Write(export.ContractName);
                        this.Write(export.DeclaringTypeRef);
                        this.Write(export.MemberRef);
                        this.Write(export.ExportedValueTypeRef);
                        this.Write(export.Metadata);
                    }
                }
            }

            private RuntimeComposition.RuntimeExport ReadRuntimeExport()
            {
                using (this.Trace("RuntimeExport"))
                {
                    uint id;
                    RuntimeComposition.RuntimeExport value;
                    if (this.TryPrepareDeserializeReusableObject(out id, out value))
                    {
                        var contractName = this.ReadString();
                        var declaringType = this.ReadTypeRef();
                        var member = this.ReadMemberRef();
                        var exportedValueType = this.ReadTypeRef();
                        var metadata = this.ReadMetadata();

                        value = new RuntimeComposition.RuntimeExport(
                            contractName,
                            declaringType,
                            member,
                            exportedValueType,
                            metadata);
                        this.OnDeserializedReusableObject(id, value);
                    }

                    return value;
                }
            }

            private void Write(RuntimeComposition.RuntimePart part)
            {
                using (this.Trace("RuntimePart"))
                {
                    this.Write(part.TypeRef);
                    this.Write(part.Exports, this.Write);
                    if (part.ImportingConstructorOrFactoryMethodRef.IsEmpty)
                    {
                        this.writer.Write(false);
                    }
                    else
                    {
                        this.writer.Write(true);
                        this.Write(part.ImportingConstructorOrFactoryMethodRef);
                        this.Write(part.ImportingConstructorArguments, this.Write);
                    }

                    this.Write(part.ImportingMembers, this.Write);
                    this.Write(part.OnImportsSatisfiedRef);
                    this.Write(part.SharingBoundary);
                }
            }

            private RuntimeComposition.RuntimePart ReadRuntimePart()
            {
                using (this.Trace("RuntimePart"))
                {
                    MethodRef importingCtor = default(MethodRef);
                    IReadOnlyList<RuntimeComposition.RuntimeImport> importingCtorArguments = ImmutableList<RuntimeComposition.RuntimeImport>.Empty;

                    var type = this.ReadTypeRef();
                    var exports = this.ReadList(this.reader, this.ReadRuntimeExport);
                    bool hasCtor = this.reader.ReadBoolean();
                    if (hasCtor)
                    {
                        importingCtor = this.ReadMethodRef();
                        importingCtorArguments = this.ReadList(this.reader, this.ReadRuntimeImport);
                    }

                    var importingMembers = this.ReadList(this.reader, this.ReadRuntimeImport);
                    var onImportsSatisfied = this.ReadMethodRef();
                    var sharingBoundary = this.ReadString();

                    return new RuntimeComposition.RuntimePart(
                        type,
                        importingCtor,
                        importingCtorArguments,
                        importingMembers,
                        exports,
                        onImportsSatisfied,
                        sharingBoundary);
                }
            }

            private void Write(RuntimeComposition.RuntimeImport import)
            {
                using (this.Trace("RuntimeImport"))
                {
                    RuntimeImportFlags flags = RuntimeImportFlags.None;
                    flags |= import.ImportingMemberRef.IsEmpty ? RuntimeImportFlags.IsParameter : 0;
                    flags |= import.IsNonSharedInstanceRequired ? RuntimeImportFlags.IsNonSharedInstanceRequired : 0;
                    flags |= import.IsExportFactory ? RuntimeImportFlags.IsExportFactory : 0;
                    flags |=
                        import.Cardinality == ImportCardinality.ExactlyOne ? RuntimeImportFlags.CardinalityExactlyOne :
                        import.Cardinality == ImportCardinality.OneOrZero ? RuntimeImportFlags.CardinalityOneOrZero : 0;
                    this.writer.Write((byte)flags);

                    if (import.ImportingMemberRef.IsEmpty)
                    {
                        this.Write(import.ImportingParameterRef);
                    }
                    else
                    {
                        this.Write(import.ImportingMemberRef);
                    }

                    this.Write(import.ImportingSiteTypeRef);
                    if (import.Cardinality == ImportCardinality.ZeroOrMore)
                    {
                        this.Write(import.ImportingSiteTypeWithoutCollectionRef);
                    }
                    else
                    {
                        if (import.ImportingSiteTypeWithoutCollectionRef != import.ImportingSiteTypeRef)
                        {
                            throw new ArgumentException($"{nameof(import.ImportingSiteTypeWithoutCollectionRef)} and {nameof(import.ImportingSiteTypeRef)} must be equal when {nameof(import.Cardinality)} is not {nameof(ImportCardinality.ZeroOrMore)}.", nameof(import));
                        }
                    }

                    this.Write(import.SatisfyingExports, this.Write);
                    this.Write(import.Metadata);
                    if (import.IsExportFactory)
                    {
                        this.Write(import.ExportFactorySharingBoundaries, this.Write);
                    }
                }
            }

            private RuntimeComposition.RuntimeImport ReadRuntimeImport()
            {
                using (this.Trace("RuntimeImport"))
                {
                    var flags = (RuntimeImportFlags)this.reader.ReadByte();
                    var cardinality =
                        flags.HasFlag(RuntimeImportFlags.CardinalityOneOrZero) ? ImportCardinality.OneOrZero :
                        flags.HasFlag(RuntimeImportFlags.CardinalityExactlyOne) ? ImportCardinality.ExactlyOne :
                        ImportCardinality.ZeroOrMore;
                    bool isExportFactory = flags.HasFlag(RuntimeImportFlags.IsExportFactory);

                    MemberRef importingMember = default(MemberRef);
                    ParameterRef importingParameter = default(ParameterRef);
                    if (flags.HasFlag(RuntimeImportFlags.IsParameter))
                    {
                        importingParameter = this.ReadParameterRef();
                    }
                    else
                    {
                        importingMember = this.ReadMemberRef();
                    }

                    var importingSiteTypeRef = this.ReadTypeRef();
                    TypeRef importingSiteTypeWithoutCollectionRef =
                        cardinality == ImportCardinality.ZeroOrMore ? this.ReadTypeRef() : importingSiteTypeRef;
                    var satisfyingExports = this.ReadList(this.reader, this.ReadRuntimeExport);
                    var metadata = this.ReadMetadata();
                    IReadOnlyList<string> exportFactorySharingBoundaries = isExportFactory
                        ? this.ReadList(this.reader, this.ReadString)
                        : ImmutableList<string>.Empty;

                    return importingMember.IsEmpty
                        ? new RuntimeComposition.RuntimeImport(
                            importingParameter,
                            importingSiteTypeRef,
                            importingSiteTypeWithoutCollectionRef,
                            cardinality,
                            satisfyingExports,
                            flags.HasFlag(RuntimeImportFlags.IsNonSharedInstanceRequired),
                            isExportFactory,
                            metadata,
                            exportFactorySharingBoundaries)
                        : new RuntimeComposition.RuntimeImport(
                            importingMember,
                            importingSiteTypeRef,
                            importingSiteTypeWithoutCollectionRef,
                            cardinality,
                            satisfyingExports,
                            flags.HasFlag(RuntimeImportFlags.IsNonSharedInstanceRequired),
                            isExportFactory,
                            metadata,
                            exportFactorySharingBoundaries);
                }
            }

            private void Write(IReadOnlyDictionary<TypeRef, RuntimeComposition.RuntimeExport> metadataTypesAndProviders)
            {
                using (this.Trace("MetadataTypesAndProviders"))
                {
                    this.WriteCompressedUInt((uint)metadataTypesAndProviders.Count);
                    foreach (var item in metadataTypesAndProviders)
                    {
                        this.Write(item.Key);
                        this.Write(item.Value);
                    }
                }
            }

            private IReadOnlyDictionary<TypeRef, RuntimeComposition.RuntimeExport> ReadMetadataViewsAndProviders()
            {
                using (this.Trace("MetadataTypesAndProviders"))
                {
                    uint count = this.ReadCompressedUInt();
                    var builder = ImmutableDictionary.CreateBuilder<TypeRef, RuntimeComposition.RuntimeExport>();
                    for (uint i = 0; i < count; i++)
                    {
                        var key = this.ReadTypeRef();
                        var value = this.ReadRuntimeExport();
                        builder.Add(key, value);
                    }

                    return builder.ToImmutable();
                }
            }
        }
    }
}
