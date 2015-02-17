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
            Requires.NotNull(catalog, "catalog");
            Requires.NotNull(cacheStream, "cacheStream");

            return Task.Run(() =>
            {
                using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(writer, catalog.Parts.Count * 4);
                    context.Write(catalog);
                    context.FinalizeObjectTableCapacity();
                }
            });
        }

        public Task<ComposableCatalog> LoadAsync(Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, "cacheStream");

            return Task.Run(() =>
            {
                using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
                {
                    var context = new SerializationContext(reader);
                    var catalog = context.ReadComposableCatalog();
                    return catalog;
                }
            });
        }

        private class SerializationContext : SerializationContextBase
        {
            internal SerializationContext(BinaryReader reader)
                : base(reader)
            {
            }

            internal SerializationContext(BinaryWriter writer, int estimatedObjectCount)
                : base(writer, estimatedObjectCount)
            {
            }

            internal void Write(ComposableCatalog catalog)
            {
                using (Trace("Catalog"))
                {
                    this.Write(catalog.Parts, this.Write);
                }
            }

            internal ComposableCatalog ReadComposableCatalog()
            {
                using (Trace("Catalog"))
                {
                    var parts = this.ReadList(this.ReadComposablePartDefinition);
                    return ComposableCatalog.Create(parts);
                }
            }

            private void Write(ComposablePartDefinition partDefinition)
            {
                using (Trace("ComposablePartDefinition"))
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
                    if (partDefinition.ImportingConstructorRef.IsEmpty)
                    {
                        writer.Write(false);
                    }
                    else
                    {
                        writer.Write(true);
                        this.Write(partDefinition.ImportingConstructorRef);
                        this.Write(partDefinition.ImportingConstructorImports, this.Write);
                    }

                    this.Write(partDefinition.CreationPolicy);
                    writer.Write(partDefinition.IsSharingBoundaryInferred);
                }
            }

            private ComposablePartDefinition ReadComposablePartDefinition()
            {
                using (Trace("ComposablePartDefinition"))
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

                    ConstructorRef importingConstructor = default(ConstructorRef);
                    IReadOnlyList<ImportDefinitionBinding> importingConstructorImports = null;
                    if (reader.ReadBoolean())
                    {
                        importingConstructor = this.ReadConstructorRef();
                        importingConstructorImports = this.ReadList(this.ReadImportDefinitionBinding);
                    }

                    var creationPolicy = this.ReadCreationPolicy();
                    var isSharingBoundaryInferred = reader.ReadBoolean();

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
                using (Trace("CreationPolicy"))
                {
                    writer.Write((byte)creationPolicy);
                }
            }

            private CreationPolicy ReadCreationPolicy()
            {
                using (Trace("CreationPolicy"))
                {
                    return (CreationPolicy)reader.ReadByte();
                }
            }

            private void Write(ExportDefinition exportDefinition)
            {
                using (Trace("ExportDefinition"))
                {
                    this.Write(exportDefinition.ContractName);
                    this.Write(exportDefinition.Metadata);
                }
            }

            private ExportDefinition ReadExportDefinition()
            {
                using (Trace("ExportDefinition"))
                {
                    var contractName = this.ReadString();
                    var metadata = this.ReadMetadata();
                    return new ExportDefinition(contractName, metadata);
                }
            }

            private void Write(ImportDefinition importDefinition)
            {
                using (Trace("ImportDefinition"))
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
                using (Trace("ImportDefinition"))
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
                using (Trace("ImportDefinitionBinding"))
                {
                    this.Write(importDefinitionBinding.ImportDefinition);
                    this.Write(importDefinitionBinding.ComposablePartTypeRef);
                    if (importDefinitionBinding.ImportingMemberRef.IsEmpty)
                    {
                        writer.Write(false);
                        this.Write(importDefinitionBinding.ImportingParameterRef);
                    }
                    else
                    {
                        writer.Write(true);
                        this.Write(importDefinitionBinding.ImportingMemberRef);
                    }
                }
            }

            private ImportDefinitionBinding ReadImportDefinitionBinding()
            {
                using (Trace("ImportDefinitionBinding"))
                {
                    var importDefinition = this.ReadImportDefinition();
                    var part = this.ReadTypeRef();

                    MemberRef member;
                    ParameterRef parameter;
                    bool isMember = reader.ReadBoolean();
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

            private enum ConstraintTypes
            {
                ImportMetadataViewConstraint,
                ExportTypeIdentityConstraint,
                PartCreationPolicyConstraint,
                ExportMetadataValueImportConstraint,
            }

            private void Write(IImportSatisfiabilityConstraint importConstraint)
            {
                using (Trace("IImportSatisfiabilityConstraint"))
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
                        throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, ConfigurationStrings.ImportConstraintTypeNotSupported, importConstraint.GetType().FullName));
                    }

                    writer.Write((byte)type);
                    switch (type)
                    {
                        case ConstraintTypes.ImportMetadataViewConstraint:
                            var importMetadataViewConstraint = (ImportMetadataViewConstraint)importConstraint;
                            this.WriteCompressedUInt((uint)importMetadataViewConstraint.Requirements.Count);
                            foreach (var item in importMetadataViewConstraint.Requirements)
                            {
                                this.Write(item.Key);
                                this.Write(item.Value.MetadatumValueType);
                                writer.Write(item.Value.IsMetadataumValueRequired);
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
                using (Trace("IImportSatisfiabilityConstraint"))
                {
                    var type = (ConstraintTypes)reader.ReadByte();
                    switch (type)
                    {
                        case ConstraintTypes.ImportMetadataViewConstraint:
                            uint count = this.ReadCompressedUInt();
                            var requirements = ImmutableDictionary.CreateBuilder<string, ImportMetadataViewConstraint.MetadatumRequirement>();
                            for (int i = 0; i < count; i++)
                            {
                                var name = this.ReadString();
                                var valueTypeRef = this.ReadTypeRef();
                                var isRequired = reader.ReadBoolean();
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
                            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, ConfigurationStrings.UnexpectedConstraintType, type));
                    }
                }
            }
        }
    }
}
