// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class CachedCatalog
    {
        protected static readonly Encoding TextEncoding = Encoding.UTF8;

        public Task SaveAsync(ComposableCatalog catalog, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(catalog, nameof(catalog));
            Requires.NotNull(cacheStream, nameof(cacheStream));

            return Task.Run(() =>
            {
                using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(writer, catalog.Parts.Count * 4, catalog.Resolver);
                    context.Write(catalog);
                    context.FinalizeObjectTableCapacity();
                }
            });
        }

        public Task<ComposableCatalog> LoadAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.NotNull(resolver, nameof(resolver));

            return Task.Run(() =>
            {
                using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(reader, resolver);
                    var catalog = context.ReadComposableCatalog();
                    return catalog;
                }
            });
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

            private enum ConstraintTypes
            {
                ImportMetadataViewConstraint,
                ExportTypeIdentityConstraint,
                PartCreationPolicyConstraint,
                ExportMetadataValueImportConstraint,
            }

            internal void Write(ComposableCatalog catalog)
            {
                using (this.Trace("Catalog"))
                {
                    this.Write(catalog.Parts, this.Write);
                }
            }

            internal ComposableCatalog ReadComposableCatalog()
            {
                using (this.Trace("Catalog"))
                {
                    var parts = this.ReadList(this.ReadComposablePartDefinition);
                    return ComposableCatalog.Create(this.Resolver).AddParts(parts);
                }
            }

            private void Write(ComposablePartDefinition partDefinition)
            {
                using (this.Trace("ComposablePartDefinition"))
                {
                    this.Write(partDefinition.TypeRef);
                    this.Write(partDefinition.Metadata);
                    this.Write(partDefinition.ExportedTypes, this.Write);

                    this.WriteCompressedUInt((uint)partDefinition.ExportingMembers.Count);
                    foreach (var exportingMember in partDefinition.ExportingMembers)
                    {
                        this.Write(exportingMember.Key);
                        this.Write(exportingMember.Value, this.Write);
                    }

                    this.Write(partDefinition.ImportingMembers, this.Write);
                    this.Write(partDefinition.SharingBoundary);
                    this.Write(partDefinition.OnImportsSatisfiedRef);
                    if (partDefinition.ImportingConstructorOrFactoryRef.IsEmpty)
                    {
                        this.writer.Write(false);
                    }
                    else
                    {
                        this.writer.Write(true);
                        this.Write(partDefinition.ImportingConstructorOrFactoryRef);
                        this.Write(partDefinition.ImportingConstructorImports, this.Write);
                    }

                    this.Write(partDefinition.CreationPolicy);
                    this.writer.Write(partDefinition.IsSharingBoundaryInferred);
                }
            }

            private ComposablePartDefinition ReadComposablePartDefinition()
            {
                using (this.Trace("ComposablePartDefinition"))
                {
                    var partType = this.ReadTypeRef();
                    var partMetadata = this.ReadMetadata();
                    var exportedTypes = this.ReadList(this.ReadExportDefinition);
                    var exportingMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();
                    uint exportedMembersCount = this.ReadCompressedUInt();
                    for (int i = 0; i < exportedMembersCount; i++)
                    {
                        var member = this.ReadMemberRef();
                        var exports = this.ReadList(this.ReadExportDefinition);
                        exportingMembers.Add(member, exports);
                    }

                    var importingMembers = this.ReadList(this.ReadImportDefinitionBinding);
                    var sharingBoundary = this.ReadString();
                    var onImportsSatisfied = this.ReadMethodRef();

                    MethodRef importingConstructor = default(MethodRef);
                    IReadOnlyList<ImportDefinitionBinding> importingConstructorImports = null;
                    if (this.reader.ReadBoolean())
                    {
                        importingConstructor = this.ReadMethodRef();
                        importingConstructorImports = this.ReadList(this.ReadImportDefinitionBinding);
                    }

                    var creationPolicy = this.ReadCreationPolicy();
                    var isSharingBoundaryInferred = this.reader.ReadBoolean();

                    var part = new ComposablePartDefinition(
                        partType,
                        partMetadata,
                        exportedTypes,
                        exportingMembers,
                        importingMembers,
                        sharingBoundary,
                        onImportsSatisfied,
                        importingConstructor,
                        importingConstructorImports,
                        creationPolicy,
                        isSharingBoundaryInferred);
                    return part;
                }
            }

            private void Write(CreationPolicy creationPolicy)
            {
                using (this.Trace("CreationPolicy"))
                {
                    this.writer.Write((byte)creationPolicy);
                }
            }

            private CreationPolicy ReadCreationPolicy()
            {
                using (this.Trace("CreationPolicy"))
                {
                    return (CreationPolicy)this.reader.ReadByte();
                }
            }

            private void Write(ExportDefinition exportDefinition)
            {
                using (this.Trace("ExportDefinition"))
                {
                    this.Write(exportDefinition.ContractName);
                    this.Write(exportDefinition.Metadata);
                }
            }

            private ExportDefinition ReadExportDefinition()
            {
                using (this.Trace("ExportDefinition"))
                {
                    var contractName = this.ReadString();
                    var metadata = this.ReadMetadata();
                    return new ExportDefinition(contractName, metadata);
                }
            }

            private void Write(ImportDefinition importDefinition)
            {
                using (this.Trace("ImportDefinition"))
                {
                    this.Write(importDefinition.ContractName);
                    this.Write(importDefinition.Cardinality);
                    this.Write(importDefinition.Metadata);
                    this.Write(importDefinition.ExportConstraints, this.Write);
                    this.Write(importDefinition.ExportFactorySharingBoundaries, this.Write);
                }
            }

            private ImportDefinition ReadImportDefinition()
            {
                using (this.Trace("ImportDefinition"))
                {
                    var contractName = this.ReadString();
                    var cardinality = this.ReadImportCardinality();
                    var metadata = this.ReadMetadata();
                    var constraints = this.ReadList(this.ReadIImportSatisfiabilityConstraint);
                    var sharingBoundaries = this.ReadList(this.ReadString);
                    return new ImportDefinition(contractName, cardinality, metadata, constraints, sharingBoundaries);
                }
            }

            private void Write(ImportDefinitionBinding importDefinitionBinding)
            {
                using (this.Trace("ImportDefinitionBinding"))
                {
                    this.Write(importDefinitionBinding.ImportDefinition);
                    this.Write(importDefinitionBinding.ComposablePartTypeRef);
                    if (importDefinitionBinding.ImportingMemberRef.IsEmpty)
                    {
                        this.writer.Write(false);
                        this.Write(importDefinitionBinding.ImportingParameterRef);
                    }
                    else
                    {
                        this.writer.Write(true);
                        this.Write(importDefinitionBinding.ImportingMemberRef);
                    }
                }
            }

            private ImportDefinitionBinding ReadImportDefinitionBinding()
            {
                using (this.Trace("ImportDefinitionBinding"))
                {
                    var importDefinition = this.ReadImportDefinition();
                    var part = this.ReadTypeRef();

                    MemberRef member;
                    ParameterRef parameter;
                    bool isMember = this.reader.ReadBoolean();
                    if (isMember)
                    {
                        member = this.ReadMemberRef();
                        return new ImportDefinitionBinding(importDefinition, part, member);
                    }
                    else
                    {
                        parameter = this.ReadParameterRef();
                        return new ImportDefinitionBinding(importDefinition, part, parameter);
                    }
                }
            }

            private void Write(IImportSatisfiabilityConstraint importConstraint)
            {
                using (this.Trace("IImportSatisfiabilityConstraint"))
                {
                    ConstraintTypes type;
                    if (importConstraint is ImportMetadataViewConstraint)
                    {
                        type = ConstraintTypes.ImportMetadataViewConstraint;
                    }
                    else if (importConstraint is ExportTypeIdentityConstraint)
                    {
                        type = ConstraintTypes.ExportTypeIdentityConstraint;
                    }
                    else if (importConstraint is PartCreationPolicyConstraint)
                    {
                        type = ConstraintTypes.PartCreationPolicyConstraint;
                    }
                    else if (importConstraint is ExportMetadataValueImportConstraint)
                    {
                        type = ConstraintTypes.ExportMetadataValueImportConstraint;
                    }
                    else
                    {
                        throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.ImportConstraintTypeNotSupported, importConstraint.GetType().FullName));
                    }

                    this.writer.Write((byte)type);
                    switch (type)
                    {
                        case ConstraintTypes.ImportMetadataViewConstraint:
                            var importMetadataViewConstraint = (ImportMetadataViewConstraint)importConstraint;
                            this.WriteCompressedUInt((uint)importMetadataViewConstraint.Requirements.Count);
                            foreach (var item in importMetadataViewConstraint.Requirements)
                            {
                                this.Write(item.Key);
                                this.Write(item.Value.MetadatumValueTypeRef);
                                this.writer.Write(item.Value.IsMetadataumValueRequired);
                            }

                            break;
                        case ConstraintTypes.ExportTypeIdentityConstraint:
                            var exportTypeIdentityConstraint = (ExportTypeIdentityConstraint)importConstraint;
                            this.Write(exportTypeIdentityConstraint.TypeIdentityName);
                            break;
                        case ConstraintTypes.PartCreationPolicyConstraint:
                            var partCreationPolicyConstraint = (PartCreationPolicyConstraint)importConstraint;
                            this.Write(partCreationPolicyConstraint.RequiredCreationPolicy);
                            break;
                        case ConstraintTypes.ExportMetadataValueImportConstraint:
                            var exportMetadataValueImportConstraint = (ExportMetadataValueImportConstraint)importConstraint;
                            this.Write(exportMetadataValueImportConstraint.Name);
                            this.WriteObject(exportMetadataValueImportConstraint.Value);
                            break;
                        default:
                            throw Assumes.NotReachable();
                    }
                }
            }

            private IImportSatisfiabilityConstraint ReadIImportSatisfiabilityConstraint()
            {
                using (this.Trace("IImportSatisfiabilityConstraint"))
                {
                    var type = (ConstraintTypes)this.reader.ReadByte();
                    switch (type)
                    {
                        case ConstraintTypes.ImportMetadataViewConstraint:
                            uint count = this.ReadCompressedUInt();
                            var requirements = ImmutableDictionary.CreateBuilder<string, ImportMetadataViewConstraint.MetadatumRequirement>();
                            for (int i = 0; i < count; i++)
                            {
                                var name = this.ReadString();
                                var valueTypeRef = this.ReadTypeRef();
                                var isRequired = this.reader.ReadBoolean();
                                requirements.Add(name, new ImportMetadataViewConstraint.MetadatumRequirement(valueTypeRef, isRequired));
                            }

                            return new ImportMetadataViewConstraint(requirements.ToImmutable());
                        case ConstraintTypes.ExportTypeIdentityConstraint:
                            string exportTypeIdentity = this.ReadString();
                            return new ExportTypeIdentityConstraint(exportTypeIdentity);
                        case ConstraintTypes.PartCreationPolicyConstraint:
                            CreationPolicy creationPolicy = this.ReadCreationPolicy();
                            return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(creationPolicy);
                        case ConstraintTypes.ExportMetadataValueImportConstraint:
                            {
                                string name = this.ReadString();
                                object value = this.ReadObject();
                                return new ExportMetadataValueImportConstraint(name, value);
                            }

                        default:
                            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.UnexpectedConstraintType, type));
                    }
                }
            }
        }
    }
}
