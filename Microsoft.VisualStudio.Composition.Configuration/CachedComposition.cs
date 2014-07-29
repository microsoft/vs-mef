namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
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
                    var catalog = this.ReadCatalog(reader);

                    // TODO: serialize/deserialize the configuration to avoid recomputing it on load.
                    var configuration = CompositionConfiguration.Create(catalog);

                    return configuration.CreateExportProviderFactory();
                }
            });
        }

        private void Write(BinaryWriter writer, ComposableCatalog catalog)
        {
            Requires.NotNull(writer, "writer");
            Requires.NotNull(catalog, "catalog");

            this.Write(writer, catalog.Parts, this.Write);
        }

        private ComposableCatalog ReadCatalog(BinaryReader reader)
        {
            Requires.NotNull(reader, "reader");

            var parts = this.ReadList(reader, this.ReadComposablePartDefinition);
            return ComposableCatalog.Create(parts);
        }

        private void Write(BinaryWriter writer, ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(writer, "writer");
            Requires.NotNull(partDefinition, "partDefinition");

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
            Requires.NotNull(reader, "reader");

            Type partType = this.ReadType(reader);
            IReadOnlyList<ExportDefinition> exportsOnType = this.ReadList(reader, this.ReadExportDefinition);
            IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> exportsOnMembers = this.ReadExportingMembers(reader);
            IReadOnlyList<ImportDefinitionBinding> imports = this.ReadList(reader, this.ReadImportDefinitionBinding);
            MethodInfo onImportsSatisfied = this.ReadMethodInfo(reader);
            IReadOnlyList<ImportDefinitionBinding> importingConstructor = this.ReadList(reader, this.ReadImportDefinitionBinding);
            CreationPolicy partCreationPolicy = (CreationPolicy)reader.ReadByte();

            // TODO: fix this to include sharing boundary and IsSharingBoundaryInferred
            return new ComposablePartDefinition(partType, exportsOnType, exportsOnMembers, imports, onImportsSatisfied, importingConstructor, partCreationPolicy);
        }

        private void Write(BinaryWriter writer, Type type)
        {
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

            return type;
        }

        private void Write(BinaryWriter writer, Assembly assembly)
        {
            writer.Write(assembly.FullName);
        }

        private Assembly ReadAssembly(BinaryReader reader)
        {
            string assemblyName = reader.ReadString();
            return Assembly.Load(assemblyName);
        }

        private void Write<T>(BinaryWriter writer, IReadOnlyCollection<T> list, Action<BinaryWriter, T> itemWriter)
        {
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

        private void Write(BinaryWriter writer, ExportDefinition exportDefinition)
        {
            writer.Write(exportDefinition.ContractName);
            this.Write(writer, exportDefinition.Metadata);
        }

        private ExportDefinition ReadExportDefinition(BinaryReader reader)
        {
            string contractName = reader.ReadString();
            IReadOnlyDictionary<string, object> metadata = this.ReadMetadata(reader);
            return new ExportDefinition(contractName, metadata);
        }

        private void Write(BinaryWriter writer, IReadOnlyDictionary<string, object> metadata)
        {
            writer.Write(metadata.Count);
            foreach (var entry in metadata)
            {
                writer.Write(entry.Key);
                this.WriteObject(writer, entry.Value);
            }
        }

        private IReadOnlyDictionary<string, object> ReadMetadata(BinaryReader reader)
        {
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
            writer.Write(exportingMembers.Count);
            foreach (var item in exportingMembers)
            {
                this.Write(writer, item.Key);
                this.Write(writer, item.Value, this.Write);
            }
        }

        private IReadOnlyDictionary<MemberInfo, IReadOnlyList<ExportDefinition>> ReadExportingMembers(BinaryReader reader)
        {
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

        private void Write(BinaryWriter writer, ImportDefinitionBinding importDefinitionBinding)
        {
            throw new NotImplementedException();
        }

        private ImportDefinitionBinding ReadImportDefinitionBinding(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        private void Write(BinaryWriter writer, MemberInfo member)
        {
            throw new NotImplementedException();
        }

        private MemberInfo ReadMemberInfo(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        private void Write(BinaryWriter writer, MethodInfo method)
        {
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

        private MethodInfo ReadMethodInfo(BinaryReader reader)
        {
            MethodInfo method = null;
            bool nonNull = reader.ReadBoolean();
            if (nonNull)
            {
                Type declaringType = this.ReadType(reader);
                int metadataToken = reader.ReadInt32();
                method = (MethodInfo)declaringType.Assembly.ManifestModule.ResolveMethod(metadataToken);
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

                        method = method.MakeGenericMethod(typeArgs);
                    }
                }
            }

            return method;
        }

        private enum ObjectType : byte
        {
            Null,
            String,
            CreationPolicy,
        }

        private void WriteObject(BinaryWriter writer, object value)
        {
            if (value == null)
            {
                this.Write(writer, ObjectType.Null);
            }
            else
            {
                Type valueType = value.GetType();
                if (valueType == typeof(string))
                {
                    this.Write(writer, ObjectType.String);
                    writer.Write((string)value);
                }
                else if (valueType == typeof(CreationPolicy)) // TODO: how do we handle arbitrary value types?
                {
                    this.Write(writer, ObjectType.CreationPolicy);
                    writer.Write((byte)(CreationPolicy)value);
                }
                else
                {
                    throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, "Objects of type {0} are not supported.", valueType.FullName));
                }
            }
        }

        private object ReadObject(BinaryReader reader)
        {
            ObjectType objectType = this.ReadObjectType(reader);
            switch (objectType)
            {
                case ObjectType.Null:
                    return null;
                case ObjectType.String:
                    return reader.ReadString();
                case ObjectType.CreationPolicy:
                    return (CreationPolicy)reader.ReadByte();
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
