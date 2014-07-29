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
    using Validation;

    public class CachedComposition : ICompositionCacheManager
    {
        private static readonly Encoding TextEncoding = Encoding.UTF8;

        public Task SaveAsync(CompositionConfiguration configuration, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, "configuration");
            Requires.NotNull(cacheStream, "cacheStream");
            Requires.Argument(cacheStream.CanWrite, "cacheStream", "Writable stream required.");

            return Task.Run(() =>
            {
                using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
                {
                    Debug.WriteLine("Start serialization of MEF cache file.");
                    this.Write(writer, configuration.Catalog);
                }
            });
        }

        public Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, "cacheStream");
            Requires.Argument(cacheStream.CanRead, "cacheStream", "Readable stream required.");

            return Task.Run(() =>
            {
                using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
                {
                    Debug.WriteLine("Start deserialization of MEF cache file.");
                    var catalog = this.ReadCatalog(reader);

                    // TODO: serialize/deserialize the configuration to avoid recomputing it on load.
                    var configuration = CompositionConfiguration.Create(catalog);

                    return configuration.CreateExportProviderFactory();
                }
            });
        }

        [Conditional("DEBUG")]
        private static void Trace(string elementName, Stream stream)
        {
            Debug.WriteLine("Serialization: {1,7} {0}", elementName, stream.Position);
        }

        private void Write(BinaryWriter writer, ComposableCatalog catalog)
        {
            Requires.NotNull(writer, "writer");
            Requires.NotNull(catalog, "catalog");

            Trace("ComposableCatalog", writer.BaseStream);
            this.Write(writer, catalog.Parts, this.Write);
        }

        private ComposableCatalog ReadCatalog(BinaryReader reader)
        {
            Requires.NotNull(reader, "reader");

            Trace("ComposableCatalog", reader.BaseStream);
            var parts = this.ReadList(reader, this.ReadComposablePartDefinition);
            return ComposableCatalog.Create(parts);
        }

        private void Write(BinaryWriter writer, ComposablePartDefinition partDefinition)
        {
            Trace("ComposablePartDefinition", writer.BaseStream);

            this.Write(writer, partDefinition.Type);
            this.Write(writer, partDefinition.ExportedTypes, this.Write);
            this.Write(writer, partDefinition.ExportingMembers);
            this.Write(writer, partDefinition.ImportingMembers, this.Write);
            this.Write(writer, partDefinition.OnImportsSatisfied);
            this.Write(writer, partDefinition.ImportingConstructor, this.Write);
            writer.Write((byte)partDefinition.CreationPolicy);
        }

        private ComposablePartDefinition ReadComposablePartDefinition(BinaryReader reader)
        {
            Trace("ComposablePartDefinition", reader.BaseStream);

            Type partType = this.ReadType(reader);
            IReadOnlyList<ExportDefinition> exportsOnType = this.ReadList(reader, this.ReadExportDefinition);
            IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> exportsOnMembers = this.ReadExportingMembers(reader);
            IReadOnlyList<ImportDefinitionBinding> imports = this.ReadList(reader, this.ReadImportDefinitionBinding);
            MethodInfo onImportsSatisfied = (MethodInfo)this.ReadMethodBase(reader);
            IReadOnlyList<ImportDefinitionBinding> importingConstructor = this.ReadList(reader, this.ReadImportDefinitionBinding);
            CreationPolicy partCreationPolicy = (CreationPolicy)reader.ReadByte();

            // TODO: fix this to include sharing boundary and IsSharingBoundaryInferred
            return new ComposablePartDefinition(partType, exportsOnType, exportsOnMembers, imports, onImportsSatisfied, importingConstructor, partCreationPolicy);
        }

        private void Write(BinaryWriter writer, Type type)
        {
            Trace("Type", writer.BaseStream);

            if (type.IsArray)
            {
                writer.Write((byte)1);
                type = type.GetElementType();
            }
            else
            {
                writer.Write((byte)0);
            }

            this.Write(writer, type.Assembly);
            writer.Write(type.MetadataToken);
            if (type.IsGenericType)
            {
                writer.Write(type.IsGenericTypeDefinition);
                foreach (Type typeArg in type.GetTypeInfo().GenericTypeArguments)
                {
                    this.Write(writer, typeArg);
                }
            }
        }

        private Type ReadType(BinaryReader reader)
        {
            Trace("Type", reader.BaseStream);

            int kind = reader.ReadByte();
            Assembly assembly = this.ReadAssembly(reader);
            int typeMetadataToken = reader.ReadInt32();
            Type type = assembly.ManifestModule.ResolveType(typeMetadataToken);
            if (type.IsGenericType)
            {
                bool isGenericTypeDefinition = reader.ReadBoolean();
                if (!isGenericTypeDefinition)
                {
                    Type[] typeArgs = new Type[type.GetTypeInfo().GenericTypeParameters.Length];
                    for (int i = 0; i < typeArgs.Length; i++)
                    {
                        typeArgs[i] = this.ReadType(reader);
                    }

                    type = type.MakeGenericType(typeArgs);
                }
            }

            switch (kind)
            {
                case 0:
                    return type;
                case 1:
                    return type.MakeArrayType();
                default:
                    throw new NotSupportedException();
            }
        }

        private void Write(BinaryWriter writer, Assembly assembly)
        {
            Trace("Assembly", writer.BaseStream);

            writer.Write(assembly.FullName);
        }

        private Assembly ReadAssembly(BinaryReader reader)
        {
            Trace("Assembly", reader.BaseStream);

            string assemblyName = reader.ReadString();
            return Assembly.Load(assemblyName);
        }

        private void Write<T>(BinaryWriter writer, IReadOnlyCollection<T> list, Action<BinaryWriter, T> itemWriter)
        {
            Trace("List<" + typeof(T).Name + ">", writer.BaseStream);

            if (list == null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(list.Count);
            foreach (var item in list)
            {
                itemWriter(writer, item);
            }
        }

        private IReadOnlyList<T> ReadList<T>(BinaryReader reader, Func<BinaryReader, T> itemReader)
        {
            Trace("List<" + typeof(T).Name + ">", reader.BaseStream);

            int count = reader.ReadInt32();
            if (count == -1)
            {
                return null;
            }

            var list = new T[count];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = itemReader(reader);
            }

            return list;
        }

        private Array ReadList(BinaryReader reader, Func<BinaryReader, object> itemReader, Type elementType)
        {
            Trace("List<" + elementType.Name + ">", reader.BaseStream);

            int count = reader.ReadInt32();
            if (count == -1)
            {
                return null;
            }

            var list = Array.CreateInstance(elementType, count);
            for (int i = 0; i < list.Length; i++)
            {
                list.SetValue(itemReader(reader), i);
            }

            return list;
        }

        private void Write(BinaryWriter writer, ExportDefinition exportDefinition)
        {
            Trace("ExportDefinition", writer.BaseStream);

            writer.Write(exportDefinition.ContractName);
            this.Write(writer, exportDefinition.Metadata);
        }

        private ExportDefinition ReadExportDefinition(BinaryReader reader)
        {
            Trace("ExportDefinition", reader.BaseStream);

            string contractName = reader.ReadString();
            IReadOnlyDictionary<string, object> metadata = this.ReadMetadata(reader);
            return new ExportDefinition(contractName, metadata);
        }

        private void Write(BinaryWriter writer, IReadOnlyDictionary<string, object> metadata)
        {
            Trace("Metadata", writer.BaseStream);

            writer.Write(metadata.Count);
            foreach (var entry in metadata)
            {
                writer.Write(entry.Key);
                this.WriteObject(writer, entry.Value);
            }
        }

        private IReadOnlyDictionary<string, object> ReadMetadata(BinaryReader reader)
        {
            Trace("Metadata", reader.BaseStream);

            int count = reader.ReadInt32();
            var metadata = ImmutableDictionary<string, object>.Empty;

            if (count > 0)
            {
                var builder = metadata.ToBuilder();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    object value = this.ReadObject(reader);
                    builder.Add(key, value);
                }

                metadata = builder.ToImmutable();
            }

            return metadata;
        }

        private void Write(BinaryWriter writer, IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> exportingMembers)
        {
            Trace("ExportingMembers", writer.BaseStream);

            writer.Write(exportingMembers.Count);
            foreach (var item in exportingMembers)
            {
                this.Write(writer, item.Key);
                this.Write(writer, item.Value, this.Write);
            }
        }

        private IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> ReadExportingMembers(BinaryReader reader)
        {
            Trace("ExportingMembers", reader.BaseStream);

            int count = reader.ReadInt32();
            var exportingMembers = ImmutableDictionary<MemberInfo, IReadOnlyList<ExportDefinition>>.Empty;
            if (count > 0)
            {
                var builder = exportingMembers.ToBuilder();
                for (int i = 0; i < count; i++)
                {
                    MemberInfo key = this.ReadMemberInfo(reader);
                    IReadOnlyList<ExportDefinition> exportDefinitions = this.ReadList(reader, this.ReadExportDefinition);
                    builder.Add(key, exportDefinitions);
                }

                exportingMembers = builder.ToImmutable();
            }

            return exportingMembers;
        }

        private void Write(BinaryWriter writer, ImportDefinition importDefinition)
        {
            Trace("ImportDefinition", writer.BaseStream);

            writer.Write(importDefinition.ContractName);
            writer.Write((byte)importDefinition.Cardinality);
            this.Write(writer, importDefinition.Metadata);
            this.Write(writer, importDefinition.ExportConstraints, this.WriteObject);
            this.Write(writer, importDefinition.ExportFactorySharingBoundaries, (w, v) => w.Write(v));
        }

        private ImportDefinition ReadImportDefinition(BinaryReader reader)
        {
            Trace("ImportDefinition", reader.BaseStream);

            var contractName = reader.ReadString();
            var cardinality = (ImportCardinality)reader.ReadByte();
            var metadata = this.ReadMetadata(reader);
            var constraints = this.ReadList<IImportSatisfiabilityConstraint>(reader, r => (IImportSatisfiabilityConstraint)this.ReadObject(r));
            var exportFactorySharingBoundaries = this.ReadList(reader, r => r.ReadString());

            return new ImportDefinition(contractName, cardinality, metadata, constraints, exportFactorySharingBoundaries);
        }

        private void Write(BinaryWriter writer, ImportDefinitionBinding importDefinitionBinding)
        {
            Trace("ImportDefinitionBinding", writer.BaseStream);

            this.Write(writer, importDefinitionBinding.ImportDefinition);
            this.Write(writer, importDefinitionBinding.ComposablePartType);
            if (importDefinitionBinding.ImportingMember != null)
            {
                writer.Write(true);
                this.Write(writer, importDefinitionBinding.ImportingMember);
            }
            else
            {
                writer.Write(false);
                this.Write(writer, importDefinitionBinding.ImportingParameter);
            }
        }

        private ImportDefinitionBinding ReadImportDefinitionBinding(BinaryReader reader)
        {
            Trace("ImportDefinitionBinding", reader.BaseStream);

            ImportDefinition importDefinition = this.ReadImportDefinition(reader);
            Type composablePartType = this.ReadType(reader);
            bool isImportingMember = reader.ReadBoolean();
            if (isImportingMember)
            {
                MemberInfo importingMember = this.ReadMemberInfo(reader);
                return new ImportDefinitionBinding(importDefinition, composablePartType, importingMember);
            }
            else
            {
                ParameterInfo importingParameter = this.ReadParameterInfo(reader);
                return new ImportDefinitionBinding(importDefinition, composablePartType, importingParameter);
            }
        }

        private void Write(BinaryWriter writer, ParameterInfo parameterInfo)
        {
            Trace("ParameterInfo", writer.BaseStream);

            var method = (MethodBase)parameterInfo.Member;
            this.Write(writer, method);
            int index = Array.IndexOf(method.GetParameters(), parameterInfo);
            writer.Write((byte)index);
        }

        private ParameterInfo ReadParameterInfo(BinaryReader reader)
        {
            Trace("ParameterInfo", reader.BaseStream);

            var member = this.ReadMethodBase(reader);
            int index = reader.ReadByte();
            ParameterInfo parameter = member.GetParameters()[index];
            return parameter;
        }

        private void Write(BinaryWriter writer, MemberInfo member)
        {
            Trace("MemberInfo", writer.BaseStream);

            var fieldInfo = member as FieldInfo;
            if (fieldInfo != null)
            {
                writer.Write((byte)1);
                this.Write(writer, member.DeclaringType.Assembly);
                writer.Write(member.MetadataToken);
                return;
            }

            var propertyInfo = member as PropertyInfo;
            if (propertyInfo != null)
            {
                writer.Write((byte)2);
                this.Write(writer, propertyInfo.DeclaringType);
                writer.Write(propertyInfo.Name);
                return;
            }

            var methodBase = member as MethodBase;
            if (methodBase != null)
            {
                writer.Write((byte)3);
                this.Write(writer, methodBase);
                return;
            }

            throw new NotSupportedException("Member type " + member.MemberType + " is not supported.");
        }

        private MemberInfo ReadMemberInfo(BinaryReader reader)
        {
            Trace("MemberInfo", reader.BaseStream);

            MemberInfo member;
            byte kind = reader.ReadByte();
            switch (kind)
            {
                case 1:
                    Assembly assembly = this.ReadAssembly(reader);
                    int metadataToken = reader.ReadInt32();
                    member = assembly.ManifestModule.ResolveMember(metadataToken);
                    break;
                case 2:
                    Type declaringType = this.ReadType(reader);
                    string propertyName = reader.ReadString();
                    member = declaringType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    break;
                case 3:
                    member = this.ReadMethodBase(reader);
                    break;
                default:
                    throw new NotSupportedException();
            }

            return member;
        }

        private void Write(BinaryWriter writer, MethodBase method)
        {
            Trace("MethodBase", writer.BaseStream);

            if (method == null)
            {
                writer.Write(false);
                return;
            }

            writer.Write(true);
            this.Write(writer, method.DeclaringType);
            writer.Write(method.MetadataToken);
            if (method.IsGenericMethod)
            {
                writer.Write(method.IsGenericMethodDefinition);
                foreach (var typeArg in method.GetGenericArguments())
                {
                    this.Write(writer, typeArg);
                }
            }
        }

        private MethodBase ReadMethodBase(BinaryReader reader)
        {
            Trace("MethodBase", reader.BaseStream);

            MethodBase method = null;
            bool nonNull = reader.ReadBoolean();
            if (nonNull)
            {
                Type declaringType = this.ReadType(reader);
                int metadataToken = reader.ReadInt32();
                method = declaringType.Assembly.ManifestModule.ResolveMethod(metadataToken);
                if (method.IsGenericMethod)
                {
                    bool isGenericMethodDefinition = reader.ReadBoolean();
                    if (!isGenericMethodDefinition)
                    {
                        Type[] typeArgs = new Type[method.GetGenericArguments().Length];
                        for (int i = 0; i < typeArgs.Length; i++)
                        {
                            typeArgs[i] = this.ReadType(reader);
                        }

                        method = ((MethodInfo)method).MakeGenericMethod(typeArgs);
                    }
                }
            }

            return method;
        }

        private void Write(BinaryWriter writer, ImportMetadataViewConstraint importMetadataViewConstraint)
        {
            Trace("ImportMetadataViewConstraint", writer.BaseStream);

            writer.Write(importMetadataViewConstraint.Requirements.Count);
            foreach (var item in importMetadataViewConstraint.Requirements)
            {
                writer.Write(item.Key);
                writer.Write(item.Value.IsMetadataumValueRequired);
                this.Write(writer, item.Value.MetadatumValueType);
            }
        }

        private ImportMetadataViewConstraint ReadImportMetadataViewConstraint(BinaryReader reader)
        {
            Trace("ImportMetadataViewConstraint", reader.BaseStream);

            int count = reader.ReadInt32();
            var requirements = ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>.Empty;
            if (count > 0)
            {
                var builder = requirements.ToBuilder();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    bool isRequired = reader.ReadBoolean();
                    Type type = this.ReadType(reader);
                    builder.Add(key, new ImportMetadataViewConstraint.MetadatumRequirement(type, isRequired));
                }

                requirements = builder.ToImmutable();
            }

            return new ImportMetadataViewConstraint(requirements);
        }

        private void Write(BinaryWriter writer, ExportTypeIdentityConstraint exportTypeIdentityConstraint)
        {
            Trace("ExportTypeIdentityConstraint", writer.BaseStream);

            writer.Write(exportTypeIdentityConstraint.TypeIdentityName);
        }

        private ExportTypeIdentityConstraint ReadExportTypeIdentityConstraint(BinaryReader reader)
        {
            Trace("ExportTypeIdentityConstraint", reader.BaseStream);

            string typeIdentityName = reader.ReadString();
            return new ExportTypeIdentityConstraint(typeIdentityName);
        }

        private void Write(BinaryWriter writer, PartCreationPolicyConstraint partCreationPolicyConstraint)
        {
            Trace("PartCreationPolicyConstraint", writer.BaseStream);
            writer.Write((byte)partCreationPolicyConstraint.RequiredCreationPolicy);
        }

        private PartCreationPolicyConstraint ReadPartCreationPolicyConstraint(BinaryReader reader)
        {
            Trace("PartCreationPolicyConstraint", reader.BaseStream);
            CreationPolicy requiredCreationPolicy = (CreationPolicy)reader.ReadByte();
            return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(requiredCreationPolicy);
        }

        private enum ObjectType : byte
        {
            Null,
            String,
            CreationPolicy,
            Type,
            Array,
            BinaryFormattedObject,
            ImportMetadataViewConstraint,
            ExportTypeIdentityConstraint,
            PartCreationPolicyConstraint,
        }

        private void WriteObject(BinaryWriter writer, object value)
        {
            if (value == null)
            {
                Trace("Object (null)", writer.BaseStream);
                this.Write(writer, ObjectType.Null);
            }
            else
            {
                Type valueType = value.GetType();
                Trace("Object (" + valueType.Name + ")", writer.BaseStream);
                if (valueType.IsArray)
                {
                    Array array = (Array)value;
                    this.Write(writer, ObjectType.Array);
                    this.Write(writer, valueType.GetElementType());
                    this.Write(writer, (object[])array, this.WriteObject);
                }
                else if (valueType == typeof(string))
                {
                    this.Write(writer, ObjectType.String);
                    writer.Write((string)value);
                }
                else if (valueType == typeof(CreationPolicy)) // TODO: how do we handle arbitrary value types?
                {
                    this.Write(writer, ObjectType.CreationPolicy);
                    writer.Write((byte)(CreationPolicy)value);
                }
                else if (typeof(Type).IsAssignableFrom(valueType))
                {
                    this.Write(writer, ObjectType.Type);
                    this.Write(writer, (Type)value);
                }
                else if (valueType == typeof(ImportMetadataViewConstraint))
                {
                    this.Write(writer, ObjectType.ImportMetadataViewConstraint);
                    this.Write(writer, (ImportMetadataViewConstraint)value);
                }
                else if (valueType == typeof(ExportTypeIdentityConstraint))
                {
                    this.Write(writer, ObjectType.ExportTypeIdentityConstraint);
                    this.Write(writer, (ExportTypeIdentityConstraint)value);
                }
                else if (valueType == typeof(PartCreationPolicyConstraint))
                {
                    this.Write(writer, ObjectType.PartCreationPolicyConstraint);
                    this.Write(writer, (PartCreationPolicyConstraint)value);
                }
                else
                {
                    this.Write(writer, ObjectType.BinaryFormattedObject);
                    var formatter = new BinaryFormatter();
                    writer.Flush();
                    formatter.Serialize(writer.BaseStream, value);
                }
            }
        }

        private object ReadObject(BinaryReader reader)
        {
            Trace("Object", reader.BaseStream);
            ObjectType objectType = this.ReadObjectType(reader);
            switch (objectType)
            {
                case ObjectType.Null:
                    return null;
                case ObjectType.Array:
                    Type elementType = this.ReadType(reader);
                    return this.ReadList(reader, this.ReadObject, elementType);
                case ObjectType.String:
                    return reader.ReadString();
                case ObjectType.CreationPolicy:
                    return (CreationPolicy)reader.ReadByte();
                case ObjectType.Type:
                    return this.ReadType(reader);
                case ObjectType.BinaryFormattedObject:
                    var formatter = new BinaryFormatter();
                    return formatter.Deserialize(reader.BaseStream);
                case ObjectType.ImportMetadataViewConstraint:
                    return this.ReadImportMetadataViewConstraint(reader);
                case ObjectType.ExportTypeIdentityConstraint:
                    return this.ReadExportTypeIdentityConstraint(reader);
                case ObjectType.PartCreationPolicyConstraint:
                    return this.ReadPartCreationPolicyConstraint(reader);
                default:
                    throw new NotImplementedException();
            }
        }

        private void Write(BinaryWriter writer, ObjectType type)
        {
            writer.Write((byte)type);
        }

        private ObjectType ReadObjectType(BinaryReader reader)
        {
            var objectType = (ObjectType)reader.ReadByte();
            return objectType;
        }
    }
}
