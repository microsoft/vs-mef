// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;
    using MessagePack.Resolvers;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class CachedCatalog
    {
        protected static readonly Encoding TextEncoding = Encoding.UTF8;

        //public Task SaveAsync(ComposableCatalog catalog, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    Requires.NotNull(catalog, nameof(catalog));
        //    Requires.NotNull(cacheStream, nameof(cacheStream));

        //    return Task.Run(() =>
        //    {
        //        using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
        //        {
        //            using (var context = new SerializationContext(writer, catalog.Parts.Count * 4, catalog.Resolver))
        //            {
        //                context.Write(catalog);
        //                context.FinalizeObjectTableCapacity();
        //            }
        //        }
        //    });
        //}

        //public Task<ComposableCatalog> LoadAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    Requires.NotNull(cacheStream, nameof(cacheStream));
        //    Requires.NotNull(resolver, nameof(resolver));

        //    return Task.Run(() =>
        //    {
        //        using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
        //        {
        //            using (var context = new SerializationContext(reader, resolver))
        //            {
        //                var catalog = context.ReadComposableCatalog();
        //                return catalog;
        //            }
        //        }
        //    });
        //}


        //SerializeAsync
        public async Task SaveAsync(ComposableCatalog catalog, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(catalog, nameof(catalog));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            //MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;

            //var options = new MessagePackSerializerOptions(MessagePack.Resolvers.ContractlessStandardResolver.Instance);



            //var options = new MessagePackSerializerOptions(MessagePack.Resolvers.DynamicObjectResolverAllowPrivate.Instance);
            //  await MessagePackSerializer.SerializeAsync(cacheStream, catalog, MessagePackSerializerOptions.Standard, cancellationToken);

            //var ii = catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints.First();

            //if (ii is ImportMetadataViewConstraint)
            //{
            //    var  uu =  (ImportMetadataViewConstraint)ii;
            //    MessagePackSerializer.Serialize(cacheStream, uu.Resolver, options);
            //}
            //else
            //{
            //    var uu = (ImportMetadataViewConstraint)ii;

            //    var lastIndex = catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints.Last();

            //    MessagePackSerializer.Serialize(cacheStream, uu.Resolver, options);
            //}


            //if (catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints.Last() is Microsoft.VisualStudio.Composition.ImportMetadataViewConstraint)
            //{
            //    var tttt = ((Microsoft.VisualStudio.Composition.ImportMetadataViewConstraint)catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints.Last()).Resolver;
            //    var res = MessagePackSerializer.Serialize(tttt, options);

            //}
            //else if(catalog.DiscoveredParts.Parts.Last().Imports.First().ImportDefinition.ExportConstraints.First() is Microsoft.VisualStudio.Composition.ImportMetadataViewConstraint)
            //{
            //    var tttt = ((Microsoft.VisualStudio.Composition.ImportMetadataViewConstraint)catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints.First()).Resolver;

            //    var res = MessagePackSerializer.Serialize(tttt, options);

            //}

            //var res2 = MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints, options);

            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.Cardinality, options);
            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportFactorySharingBoundaries, options);
            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.Metadata, options);
            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ContractName, options);
            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition.ExportConstraints, options);
            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition., options);

            //var ImportDefinition = catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition;
            // MessagePackSerializer.Serialize(ImportDefinition, options);


            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition, options);


            //var res2 = MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition, options);


            //var ImportDefinition = catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition;
            //MessagePackSerializer.Serialize(ImportDefinition, options);

            ///

            // var ImportDefinition = catalog.DiscoveredParts.Parts.First().Imports.First().ComposablePartType;


            var options = new MessagePackSerializerOptions(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        //     options = new MessagePackSerializerOptions(MessagePack.Resolvers.StandardResolverAllowPrivate.Instance);
           // options = new MessagePackSerializerOptions(MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Instance);


            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ComposablePartTypeRef, options); //here


            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ComposablePartTypeRef, options);


            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportDefinition, options);
            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ComposablePartType, options);


            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ExportFactoryType, options);

            

            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingMember, options); //todo Ankit ignore MemberInfo

            var ty = MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingMemberRef, options);
            var convertedBack = MessagePackSerializer.Deserialize<MemberRef?>(ty, options);
            var strin = MessagePackSerializer.ConvertToJson(ty);

            foreach (ComposablePartDefinition yuParts in catalog.Parts)
            {
                TestSerializeDeseriaizeTest(yuParts.Metadata, "Metadata");

                TestSerializeDeseriaizeTest(yuParts.ExportedTypes, "ExportedTypes");

                TestSerializeDeseriaizeTest(yuParts.ExportingMembers, "ExportingMembers");

                TestSerializeDeseriaizeTest(yuParts.OnImportsSatisfiedMethodRefs, "OnImportsSatisfiedMethodRefs");

                TestSerializeDeseriaizeTest(yuParts.SharingBoundary, "SharingBoundary");

                TestSerializeDeseriaizeTest(yuParts.ImportingMembers, "ImportingMembers");

                TestSerializeDeseriaizeTest(yuParts.ImportingConstructorImports, "ImportingConstructorImports");

                TestSerializeDeseriaizeTest(yuParts.ImportingConstructorOrFactoryRef, "ImportingConstructorOrFactoryRef");

                TestSerializeDeseriaizeTest(yuParts.ImportingConstructorOrFactoryRef, "ImportingConstructorOrFactoryRef");

                TestSerializeDeseriaizeTest(yuParts.TypeRef, "TypeRef");

                TestSerializeDeseriaizeTest(yuParts, "Parts");

                foreach (ImportDefinitionBinding ImportingMembers in yuParts.ImportingMembers)
                {
                    TestSerializeDeseriaizeTest(ImportingMembers, "ImportingMembers");
                }

                foreach (ImportDefinitionBinding yu in yuParts.ImportingConstructorImports)
                {
                    TestSerializeDeseriaizeTest<ImportDefinitionBinding?>(yu, "ImportingConstructorImports - ImportDefinitionBinding");
                }

                foreach (MethodRef onImportsSatisfiedMethods in yuParts.OnImportsSatisfiedMethodRefs)
                {
                    TestSerializeDeseriaizeTest(onImportsSatisfiedMethods, "OnImportsSatisfiedMethodRefs");
                }

                foreach (var yu in yuParts.Imports)
                {
                    var ty2 = MessagePackSerializer.Serialize(yu.ImportingMemberRef, options);
                    var convertedBack2 = MessagePackSerializer.Deserialize<MemberRef?>(ty2, options);
                    var strin22 = MessagePackSerializer.ConvertToJson(ty2);
                }

            }


            TestSerializeDeseriaizeTest(catalog.Resolver, "Resolver");            


            foreach (ComposablePartDefinition yuParts in catalog.DiscoveredParts.Parts)
            {
                foreach (var yu in yuParts.Imports)
                {
                    var ty2 = MessagePackSerializer.Serialize(yu.ImportingMemberRef, options);
                    var convertedBack2 = MessagePackSerializer.Deserialize<MemberRef?>(ty2, options);
                    var strin22 = MessagePackSerializer.ConvertToJson(ty2);
                }

                foreach (ImportDefinitionBinding yu in yuParts.ImportingConstructorImports)
                {
                    TestSerializeDeseriaizeTest<ImportDefinitionBinding?>(yu, "ImportingConstructorImports - ImportDefinitionBinding");
                }

                foreach (ImportDefinitionBinding ImportingMembers in yuParts.ImportingMembers)
                {
                    TestSerializeDeseriaizeTest(ImportingMembers, "ImportingMembers");
                }

                foreach (MethodRef onImportsSatisfiedMethods in yuParts.OnImportsSatisfiedMethodRefs)
                {
                    TestSerializeDeseriaizeTest(onImportsSatisfiedMethods, "OnImportsSatisfiedMethodRefs");
                }

                TestSerializeDeseriaizeTest(yuParts.ImportingConstructorOrFactoryRef, "ImportingConstructorOrFactoryRef");

                TestSerializeDeseriaizeTest(yuParts.SharingBoundary, "SharingBoundary");

                TestSerializeDeseriaizeTest(yuParts.Metadata, "Metadata");


                TestSerializeDeseriaizeTest(yuParts.ExportingMembers, "ExportingMembers");

                TestSerializeDeseriaizeTest(yuParts.ExportedTypes, "ExportedTypes");

                TestSerializeDeseriaizeTest(yuParts.TypeRef, "TypeRef");

                TestSerializeDeseriaizeTest(yuParts, "Parts");
            }



            //MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingParameter, options); can be ignore as it can be accessed from other propes

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingParameterRef, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingSiteElementType, options);
            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingSiteElementTypeRef, options);
            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingSiteType, options);
            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingSiteTypeRef, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingSiteTypeWithoutCollection, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().ImportingSiteTypeWithoutCollectionRef, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().IsExportFactory, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().IsLazy, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First().MetadataType, options);

            var ty1 = MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports.First(), options);
            var strin2 = MessagePackSerializer.ConvertToJson(ty1);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First().Imports, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts.First(), options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts.Parts, options);

            MessagePackSerializer.Serialize(catalog.DiscoveredParts, options);

            MessagePackSerializer.Serialize(catalog, options);

            var filestream = new FileStream(@"C:\bTemp\test.txt", FileMode.Create);
            await MessagePackSerializer.SerializeAsync(filestream, catalog, options, cancellationToken);
            filestream.Dispose();


            //using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            //{
            //    // Create a StreamWriter using the FileStream
            //    using (StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
            //    {
            //        // Write data to the file
            //        streamWriter.WriteLine(data);
            //    }
            //}


            await MessagePackSerializer.SerializeAsync(cacheStream, catalog, options, cancellationToken);

            //await MessagePackSerializer.SerializeAsync(cacheStream, catalog, options, cancellationToken);

            void TestSerializeDeseriaizeTest<T>(T objectToDesrialize, string name)
            {
                var temp3 = MessagePackSerializer.Serialize(objectToDesrialize, options);
                var convertedBackTemp2 = MessagePackSerializer.Deserialize<T?>(temp3, options);
                var strinTemp = MessagePackSerializer.ConvertToJson(temp3);

            }

        }

        //DeserializeAsync
        public async Task<ComposableCatalog> LoadAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.NotNull(resolver, nameof(resolver));

            Microsoft.VisualStudio.Composition.MapperTEst.Resolver = resolver;

            var options = new MessagePackSerializerOptions(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            ComposableCatalog catalog = await MessagePackSerializer.DeserializeAsync<ComposableCatalog>(cacheStream, options, cancellationToken);

            return catalog;
        }


        private class SerializationContext : SerializationContextBase
        {
            private readonly Func<IImportSatisfiabilityConstraint?> readIImportSatisfiabilityConstraintDelegate;
            private readonly Func<ImportDefinitionBinding> readImportDefinitionBindingDelegate;
            private readonly Func<ExportDefinition> readExportDefinitionDelegate;

            internal SerializationContext(BinaryReader reader, Resolver resolver)
                : base(reader, resolver)
            {
                this.readIImportSatisfiabilityConstraintDelegate = this.ReadIImportSatisfiabilityConstraint;
                this.readImportDefinitionBindingDelegate = this.ReadImportDefinitionBinding;
                this.readExportDefinitionDelegate = this.ReadExportDefinition;
            }

            internal SerializationContext(BinaryWriter writer, int estimatedObjectCount, Resolver resolver)
                : base(writer, estimatedObjectCount, resolver)
            {
                this.readIImportSatisfiabilityConstraintDelegate = this.ReadIImportSatisfiabilityConstraint;
                this.readImportDefinitionBindingDelegate = this.ReadImportDefinitionBinding;
                this.readExportDefinitionDelegate = this.ReadExportDefinition;
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
                using (this.Trace(nameof(ComposableCatalog)))
                {
                    this.Write(catalog.Parts, this.Write);
                }
            }

            internal ComposableCatalog ReadComposableCatalog()
            {
                using (this.Trace(nameof(ComposableCatalog)))
                {
                    var parts = this.ReadList(this.ReadComposablePartDefinition);
                    return ComposableCatalog.Create(this.Resolver).AddParts(parts);
                }
            }

            private void Write(ComposablePartDefinition partDefinition)
            {
                using (this.Trace(nameof(ComposablePartDefinition)))
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
                    this.Write(partDefinition.OnImportsSatisfiedMethodRefs, this.Write);
                    if (partDefinition.ImportingConstructorOrFactoryRef == null)
                    {
                        this.writer!.Write(false);
                    }
                    else
                    {
                        this.writer!.Write(true);
                        this.Write(partDefinition.ImportingConstructorOrFactoryRef);
                        this.Write(partDefinition.ImportingConstructorImports!, this.Write);
                    }

                    this.Write(partDefinition.CreationPolicy);
                    this.writer.Write(partDefinition.IsSharingBoundaryInferred);
                }
            }

            private ComposablePartDefinition ReadComposablePartDefinition()
            {
                using (this.Trace(nameof(ComposablePartDefinition)))
                {
                    var partType = this.ReadTypeRef()!;
                    var partMetadata = this.ReadMetadata();
                    var exportedTypes = this.ReadList(this.readExportDefinitionDelegate);
                    var exportingMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();
                    uint exportedMembersCount = this.ReadCompressedUInt();
                    for (int i = 0; i < exportedMembersCount; i++)
                    {
                        var member = this.ReadMemberRef()!;
                        var exports = this.ReadList(this.readExportDefinitionDelegate);
                        exportingMembers.Add(member, exports);
                    }

                    var importingMembers = this.ReadList(this.readImportDefinitionBindingDelegate);
                    var sharingBoundary = this.ReadString();
                    IReadOnlyList<MethodRef> onImportsSatisfiedMethods = this.ReadList(this.ReadMethodRef)!;

                    MethodRef? importingConstructor = default(MethodRef);
                    IReadOnlyList<ImportDefinitionBinding>? importingConstructorImports = null;
                    if (this.reader!.ReadBoolean())
                    {
                        importingConstructor = this.ReadMethodRef();
                        importingConstructorImports = this.ReadList(this.readImportDefinitionBindingDelegate);
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
                        onImportsSatisfiedMethods,
                        importingConstructor,
                        importingConstructorImports,
                        creationPolicy,
                        isSharingBoundaryInferred);
                    return part;
                }
            }

            private void Write(CreationPolicy creationPolicy)
            {
                using (this.Trace(nameof(CreationPolicy)))
                {
                    this.writer!.Write((byte)creationPolicy);
                }
            }

            private CreationPolicy ReadCreationPolicy()
            {
                using (this.Trace(nameof(CreationPolicy)))
                {
                    return (CreationPolicy)this.reader!.ReadByte();
                }
            }

            private void Write(ExportDefinition exportDefinition)
            {
                using (this.Trace(nameof(ExportDefinition)))
                {
                    this.Write(exportDefinition.ContractName);
                    this.Write(exportDefinition.Metadata);
                }
            }

            private ExportDefinition ReadExportDefinition()
            {
                using (this.Trace(nameof(ExportDefinition)))
                {
                    var contractName = this.ReadString()!;
                    var metadata = this.ReadMetadata();
                    return new ExportDefinition(contractName, metadata);
                }
            }

            private void Write(ImportDefinition importDefinition)
            {
                using (this.Trace(nameof(ImportDefinition)))
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
                using (this.Trace(nameof(ImportDefinition)))
                {
                    var contractName = this.ReadString()!;
                    var cardinality = this.ReadImportCardinality();
                    var metadata = this.ReadMetadata();
                    var constraints = this.ReadList(this.readIImportSatisfiabilityConstraintDelegate);
                    var sharingBoundaries = this.ReadList(this.readStringDelegate);
                    return new ImportDefinition(contractName, cardinality, metadata, constraints!, sharingBoundaries!);
                }
            }

            private void Write(ImportDefinitionBinding importDefinitionBinding)
            {
                using (this.Trace(nameof(ImportDefinitionBinding)))
                {
                    this.Write(importDefinitionBinding.ImportDefinition);
                    this.Write(importDefinitionBinding.ComposablePartTypeRef);
                    this.Write(importDefinitionBinding.ImportingSiteTypeRef);
                    this.Write(importDefinitionBinding.ImportingSiteTypeWithoutCollectionRef);

                    if (importDefinitionBinding.ImportingMemberRef == null)
                    {
                        this.writer!.Write(false);
                        this.Write(importDefinitionBinding.ImportingParameterRef);
                    }
                    else
                    {
                        this.writer!.Write(true);
                        this.Write(importDefinitionBinding.ImportingMemberRef);
                    }
                }
            }

            private ImportDefinitionBinding ReadImportDefinitionBinding()
            {
                using (this.Trace(nameof(ImportDefinitionBinding)))
                {
                    var importDefinition = this.ReadImportDefinition();
                    var part = this.ReadTypeRef()!;
                    var importingSiteTypeRef = this.ReadTypeRef()!;
                    var importingSiteTypeWithoutCollectionRef = this.ReadTypeRef()!;

                    MemberRef? member;
                    ParameterRef? parameter;
                    bool isMember = this.reader!.ReadBoolean();
                    if (isMember)
                    {
                        member = this.ReadMemberRef()!;
                        return new ImportDefinitionBinding(importDefinition, part, member, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
                    }
                    else
                    {
                        parameter = this.ReadParameterRef()!;
                        return new ImportDefinitionBinding(importDefinition, part, parameter, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
                    }
                }
            }

            private void Write(IImportSatisfiabilityConstraint importConstraint)
            {
                using (this.Trace(nameof(IImportSatisfiabilityConstraint)))
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

                    this.writer!.Write((byte)type);
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

            private IImportSatisfiabilityConstraint? ReadIImportSatisfiabilityConstraint()
            {
                using (this.Trace(nameof(IImportSatisfiabilityConstraint)))
                {
                    var type = (ConstraintTypes)this.reader!.ReadByte();
                    switch (type)
                    {
                        case ConstraintTypes.ImportMetadataViewConstraint:
                            uint count = this.ReadCompressedUInt();
                            var requirements = ImmutableDictionary.CreateBuilder<string, ImportMetadataViewConstraint.MetadatumRequirement>();
                            for (int i = 0; i < count; i++)
                            {
                                var name = this.ReadString()!;
                                var valueTypeRef = this.ReadTypeRef()!;
                                var isRequired = this.reader.ReadBoolean();
                                requirements.Add(name, new ImportMetadataViewConstraint.MetadatumRequirement(valueTypeRef, isRequired));
                            }

                            return new ImportMetadataViewConstraint(requirements.ToImmutable(), this.Resolver);
                        case ConstraintTypes.ExportTypeIdentityConstraint:
                            string? exportTypeIdentity = this.ReadString()!;
                            return new ExportTypeIdentityConstraint(exportTypeIdentity);
                        case ConstraintTypes.PartCreationPolicyConstraint:
                            CreationPolicy creationPolicy = this.ReadCreationPolicy();
                            return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(creationPolicy);
                        case ConstraintTypes.ExportMetadataValueImportConstraint:
                            {
                                string? name = this.ReadString()!;
                                object? value = this.ReadObject();
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
