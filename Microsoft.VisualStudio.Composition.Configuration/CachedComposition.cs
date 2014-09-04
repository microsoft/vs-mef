#if DEBUG
////#define TRACESTATS
////#define TRACESERIALIZATION
#endif

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
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    public class CachedComposition : ICompositionCacheManager, IRuntimeCompositionCacheManager
    {
        private static readonly Encoding TextEncoding = Encoding.UTF8;

        public Task SaveAsync(CompositionConfiguration configuration, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, "configuration");
            Requires.NotNull(cacheStream, "cacheStream");
            Requires.Argument(cacheStream.CanWrite, "cacheStream", "Writable stream required.");

            return Task.Run(async delegate
            {
                var compositionRuntime = RuntimeComposition.CreateRuntimeComposition(configuration);

                await this.SaveAsync(compositionRuntime, cacheStream, cancellationToken);
            });
        }

        public Task SaveAsync(RuntimeComposition composition, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(composition, "composition");
            Requires.NotNull(cacheStream, "cacheStream");
            Requires.Argument(cacheStream.CanWrite, "cacheStream", "Writable stream required.");

            return Task.Run(() =>
            {
                using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(writer);
                    context.Write(composition);
                }
            });
        }

        public Task<RuntimeComposition> LoadRuntimeCompositionAsync(Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, "cacheStream");
            Requires.Argument(cacheStream.CanRead, "cacheStream", "Readable stream required.");

            return Task.Run(() =>
            {
                using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(reader);
                    var runtimeComposition = context.ReadRuntimeComposition();
                    return runtimeComposition;
                }
            });
        }

        public async Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var runtimeComposition = await this.LoadRuntimeCompositionAsync(cacheStream, cancellationToken);
            return runtimeComposition.CreateExportProviderFactory();
        }

        private class SerializationContext : SerializationContextBase
        {
            internal SerializationContext(BinaryReader reader)
                : base(reader)
            {
            }

            internal SerializationContext(BinaryWriter writer)
                : base(writer)
            {
            }

            internal void Write(RuntimeComposition compositionRuntime)
            {
                Requires.NotNull(writer, "writer");
                Requires.NotNull(compositionRuntime, "compositionRuntime");

                using (Trace("RuntimeComposition", writer.BaseStream))
                {
                    this.Write(compositionRuntime.Parts, this.Write);
                    this.Write(compositionRuntime.MetadataViewsAndProviders);
                }

                this.TraceStats();
            }

            internal RuntimeComposition ReadRuntimeComposition()
            {
                Requires.NotNull(reader, "reader");

                RuntimeComposition result;
                using (Trace("RuntimeComposition", reader.BaseStream))
                {
                    var parts = this.ReadList(reader, this.ReadRuntimePart);
                    var metadataViewsAndProviders = this.ReadMetadataViewsAndProviders();

                    result = RuntimeComposition.CreateRuntimeComposition(parts, metadataViewsAndProviders);
                }

                this.TraceStats();
                return result;
            }

            private void Write(RuntimeComposition.RuntimeExport export)
            {
                using (Trace("RuntimeExport", writer.BaseStream))
                {
                    if (this.TryPrepareSerializeReusableObject(export))
                    {
                        this.Write(export.ContractName);
                        this.Write(export.DeclaringType);
                        this.Write(export.MemberRef);
                        this.Write(export.ExportedValueType);
                        this.Write(export.Metadata);
                    }
                }
            }

            private RuntimeComposition.RuntimeExport ReadRuntimeExport()
            {
                using (Trace("RuntimeExport", reader.BaseStream))
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
                using (Trace("RuntimePart", writer.BaseStream))
                {
                    this.Write(part.Type);
                    this.Write(part.Exports, this.Write);
                    if (part.ImportingConstructorRef.IsEmpty)
                    {
                        writer.Write(false);
                    }
                    else
                    {
                        writer.Write(true);
                        this.Write(part.ImportingConstructorRef);
                        this.Write(part.ImportingConstructorArguments, this.Write);
                    }

                    this.Write(part.ImportingMembers, this.Write);
                    this.Write(part.OnImportsSatisfiedRef);
                    this.Write(part.SharingBoundary);
                }
            }

            private RuntimeComposition.RuntimePart ReadRuntimePart()
            {
                using (Trace("RuntimePart", reader.BaseStream))
                {
                    ConstructorRef importingCtor = default(ConstructorRef);
                    IReadOnlyList<RuntimeComposition.RuntimeImport> importingCtorArguments = ImmutableList<RuntimeComposition.RuntimeImport>.Empty;

                    var type = this.ReadTypeRef();
                    var exports = this.ReadList(reader, this.ReadRuntimeExport);
                    bool hasCtor = reader.ReadBoolean();
                    if (hasCtor)
                    {
                        importingCtor = this.ReadConstructorRef();
                        importingCtorArguments = this.ReadList(reader, this.ReadRuntimeImport);
                    }

                    var importingMembers = this.ReadList(reader, this.ReadRuntimeImport);
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

            private enum RuntimeImportFlags : byte
            {
                None = 0x00,
                IsNonSharedInstanceRequired = 0x01,
                IsExportFactory = 0x02,
                CardinalityExactlyOne = 0x04,
                CardinalityOneOrZero = 0x08,
                IsParameter = 0x10,
            }

            private void Write(RuntimeComposition.RuntimeImport import)
            {
                using (Trace("RuntimeImport", writer.BaseStream))
                {
                    RuntimeImportFlags flags = RuntimeImportFlags.None;
                    flags |= import.ImportingMemberRef.IsEmpty ? RuntimeImportFlags.IsParameter : 0;
                    flags |= import.IsNonSharedInstanceRequired ? RuntimeImportFlags.IsNonSharedInstanceRequired : 0;
                    flags |= import.IsExportFactory ? RuntimeImportFlags.IsExportFactory : 0;
                    flags |=
                        import.Cardinality == ImportCardinality.ExactlyOne ? RuntimeImportFlags.CardinalityExactlyOne :
                        import.Cardinality == ImportCardinality.OneOrZero ? RuntimeImportFlags.CardinalityOneOrZero : 0;
                    writer.Write((byte)flags);

                    if (import.ImportingMemberRef.IsEmpty)
                    {
                        this.Write(import.ImportingParameterRef);
                    }
                    else
                    {
                        this.Write(import.ImportingMemberRef);
                    }

                    this.Write(import.ImportingSiteTypeRef);
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
                using (Trace("RuntimeImport", reader.BaseStream))
                {
                    var flags = (RuntimeImportFlags)reader.ReadByte();
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
                    var satisfyingExports = this.ReadList(reader, this.ReadRuntimeExport);
                    var metadata = this.ReadMetadata();
                    IReadOnlyList<string> exportFactorySharingBoundaries = isExportFactory
                        ? this.ReadList(reader, this.ReadString)
                        : ImmutableList<string>.Empty;

                    return importingMember.IsEmpty
                        ? new RuntimeComposition.RuntimeImport(
                            importingParameter,
                            importingSiteTypeRef,
                            cardinality,
                            satisfyingExports,
                            flags.HasFlag(RuntimeImportFlags.IsNonSharedInstanceRequired),
                            isExportFactory,
                            metadata,
                            exportFactorySharingBoundaries)
                        : new RuntimeComposition.RuntimeImport(
                            importingMember,
                            importingSiteTypeRef,
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
                using (Trace("MetadataTypesAndProviders", writer.BaseStream))
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
                using (Trace("MetadataTypesAndProviders", reader.BaseStream))
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
