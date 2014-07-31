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
                var compositionRuntime = RuntimeComposition.CreateRuntimeComposition(configuration);

                using (var writer = new BinaryWriter(cacheStream, TextEncoding, leaveOpen: true))
                {
                    Debug.WriteLine("Start serialization of MEF cache file.");
                    this.Write(writer, compositionRuntime);
                }
            });
        }

        public Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, "cacheStream");
            Requires.Argument(cacheStream.CanRead, "cacheStream", "Readable stream required.");

            return Task.Run<IExportProviderFactory>(() =>
            {
                using (var reader = new BinaryReader(cacheStream, TextEncoding, leaveOpen: true))
                {
                    Debug.WriteLine("Start deserialization of MEF cache file.");
                    var runtimeComposition = this.ReadRuntimeComposition(reader);

                    return new RuntimeExportProviderFactory(runtimeComposition);
                }
            });
        }

        [Conditional("DEBUG")]
        private static void Trace(string elementName, Stream stream)
        {
            Debug.WriteLine("Serialization: {1,7} {0}", elementName, stream.Position);
        }

        private void Write(BinaryWriter writer, RuntimeComposition compositionRuntime)
        {
            Requires.NotNull(writer, "writer");
            Requires.NotNull(compositionRuntime, "compositionRuntime");
            Trace("RuntimeComposition", writer.BaseStream);

            this.Write(writer, compositionRuntime.Parts, this.Write);
        }

        private RuntimeComposition ReadRuntimeComposition(BinaryReader reader)
        {
            Requires.NotNull(reader, "reader");
            Trace("RuntimeComposition", reader.BaseStream);

            var parts = this.ReadList(reader, this.ReadRuntimePart);
            return RuntimeComposition.CreateRuntimeComposition(parts);
        }

        private void Write(BinaryWriter writer, RuntimeComposition.RuntimeExport export)
        {
            Trace("RuntimeExport", writer.BaseStream);

            writer.Write(export.ContractName);
            this.Write(writer, export.DeclaringType);
            this.Write(writer, export.Member);
            this.Write(writer, export.ExportedValueType);
            this.Write(writer, export.Metadata);
        }

        private RuntimeComposition.RuntimeExport ReadRuntimeExport(BinaryReader reader)
        {
            Trace("RuntimeExport", reader.BaseStream);

            var contractName = reader.ReadString();
            var declaringType = this.ReadTypeRef(reader);
            var member = this.ReadMemberRef(reader);
            var exportedValueType = this.ReadTypeRef(reader);
            var metadata = this.ReadMetadata(reader);

            return new RuntimeComposition.RuntimeExport(
                contractName,
                declaringType,
                member,
                exportedValueType,
                metadata);
        }

        private void Write(BinaryWriter writer, RuntimeComposition.RuntimePart part)
        {
            Trace("RuntimePart", writer.BaseStream);

            this.Write(writer, part.Type);
            this.Write(writer, part.Exports, this.Write);
            this.Write(writer, part.ImportingConstructor);
            this.Write(writer, part.ImportingConstructorArguments, this.Write);
            this.Write(writer, part.ImportingMembers, this.Write);
            this.Write(writer, part.OnImportsSatisfied);
            this.Write(writer, part.SharingBoundary);
        }

        private RuntimeComposition.RuntimePart ReadRuntimePart(BinaryReader reader)
        {
            Trace("RuntimePart", reader.BaseStream);

            var type = this.ReadTypeRef(reader);
            var exports = this.ReadList(reader, this.ReadRuntimeExport);
            var importingCtor = this.ReadConstructorRef(reader);
            var importingCtorArguments = this.ReadList(reader, this.ReadRuntimeImport);
            var importingMembers = this.ReadList(reader, this.ReadRuntimeImport);
            var onImportsSatisfied = this.ReadMethodRef(reader);
            var sharingBoundary = this.ReadString(reader);

            return new RuntimeComposition.RuntimePart(
                type,
                importingCtor,
                importingCtorArguments,
                importingMembers,
                exports,
                onImportsSatisfied,
                sharingBoundary);
        }

        private void Write(BinaryWriter writer, MethodRef methodRef)
        {
            Trace("MethodRef", writer.BaseStream);

            if (methodRef.IsEmpty)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                this.Write(writer, methodRef.DeclaringType);
                writer.Write(methodRef.MetadataToken);
                this.Write(writer, methodRef.GenericMethodArguments, this.Write);
            }
        }

        private MethodRef ReadMethodRef(BinaryReader reader)
        {
            Trace("MethodRef", reader.BaseStream);

            byte nullCheck = reader.ReadByte();
            if (nullCheck == 1)
            {
                var declaringType = this.ReadTypeRef(reader);
                var metadataToken = reader.ReadInt32();
                var genericMethodArguments = this.ReadList(reader, this.ReadTypeRef);
                return new MethodRef(declaringType, metadataToken, genericMethodArguments.ToImmutableArray());
            }
            else
            {
                return default(MethodRef);
            }
        }

        private void Write(BinaryWriter writer, MemberRef memberRef)
        {
            Trace("MemberRef", writer.BaseStream);

            if (memberRef.IsConstructor)
            {
                writer.Write((byte)1);
                this.Write(writer, memberRef.Constructor);
            }
            else if (memberRef.IsField)
            {
                writer.Write((byte)2);
                this.Write(writer, memberRef.Field);
            }
            else if (memberRef.IsProperty)
            {
                writer.Write((byte)3);
                this.Write(writer, memberRef.Property);
            }
            else if (memberRef.IsMethod)
            {
                writer.Write((byte)4);
                this.Write(writer, memberRef.Method);
            }
            else
            {
                writer.Write((byte)0);
            }
        }

        private MemberRef ReadMemberRef(BinaryReader reader)
        {
            Trace("MemberRef", reader.BaseStream);

            int kind = reader.ReadByte();
            switch (kind)
            {
                case 0:
                    return default(MemberRef);
                case 1:
                    return new MemberRef(this.ReadConstructorRef(reader));
                case 2:
                    return new MemberRef(this.ReadFieldRef(reader));
                case 3:
                    return new MemberRef(this.ReadPropertyRef(reader));
                case 4:
                    return new MemberRef(this.ReadMethodRef(reader));
                default:
                    throw new NotSupportedException();
            }
        }

        private void Write(BinaryWriter writer, PropertyRef propertyRef)
        {
            Trace("PropertyRef", writer.BaseStream);

            this.Write(writer, propertyRef.DeclaringType);
            writer.Write(propertyRef.MetadataToken);

            byte flags = 0;
            flags |= propertyRef.GetMethodMetadataToken.HasValue ? (byte)0x1 : (byte)0x0;
            flags |= propertyRef.SetMethodMetadataToken.HasValue ? (byte)0x2 : (byte)0x0;
            writer.Write(flags);

            if (propertyRef.GetMethodMetadataToken.HasValue)
            {
                writer.Write(propertyRef.GetMethodMetadataToken.Value);
            }

            if (propertyRef.SetMethodMetadataToken.HasValue)
            {
                writer.Write(propertyRef.SetMethodMetadataToken.Value);
            }
        }

        private PropertyRef ReadPropertyRef(BinaryReader reader)
        {
            Trace("PropertyRef", reader.BaseStream);

            var declaringType = this.ReadTypeRef(reader);
            var metadataToken = reader.ReadInt32();

            byte flags = reader.ReadByte();
            int? getter = null, setter = null;
            if ((flags & 0x1) != 0)
            {
                getter = reader.ReadInt32();
            }

            if ((flags & 0x2) != 0)
            {
                setter = reader.ReadInt32();
            }

            return new PropertyRef(
                declaringType,
                metadataToken,
                getter,
                setter);
        }

        private void Write(BinaryWriter writer, FieldRef fieldRef)
        {
            Trace("FieldRef", writer.BaseStream);

            writer.Write(!fieldRef.IsEmpty);
            if (!fieldRef.IsEmpty)
            {
                this.Write(writer, fieldRef.AssemblyName);
                writer.Write(fieldRef.MetadataToken);
            }
        }

        private FieldRef ReadFieldRef(BinaryReader reader)
        {
            Trace("FieldRef", reader.BaseStream);

            if (reader.ReadBoolean())
            {
                var assemblyName = this.ReadAssemblyName(reader);
                int metadataToken = reader.ReadInt32();
                return new FieldRef(assemblyName, metadataToken);
            }
            else
            {
                return default(FieldRef);
            }
        }

        private void Write(BinaryWriter writer, ParameterRef parameterRef)
        {
            Trace("ParameterRef", writer.BaseStream);

            writer.Write(!parameterRef.IsEmpty);
            if (!parameterRef.IsEmpty)
            {
                this.Write(writer, parameterRef.AssemblyName);
                writer.Write(parameterRef.MethodMetadataToken);
                writer.Write((byte)parameterRef.ParameterIndex);
            }
        }

        private ParameterRef ReadParameterRef(BinaryReader reader)
        {
            Trace("ParameterRef", reader.BaseStream);

            if (reader.ReadBoolean())
            {
                var assemblyName = this.ReadAssemblyName(reader);
                int metadataToken = reader.ReadInt32();
                var parameterIndex = reader.ReadByte();
                return new ParameterRef(assemblyName, metadataToken, parameterIndex);
            }
            else
            {
                return default(ParameterRef);
            }
        }

        private void Write(BinaryWriter writer, RuntimeComposition.RuntimeImport import)
        {
            Trace("RuntimeImport", writer.BaseStream);

            writer.Write(import.ImportingMemberRef.IsEmpty ? (byte)2 : (byte)1);
            if (import.ImportingMemberRef.IsEmpty)
            {
                this.Write(writer, import.ImportingParameterRef);
            }
            else
            {
                this.Write(writer, import.ImportingMemberRef);
            }

            this.Write(writer, import.Cardinality);
            this.Write(writer, import.SatisfyingExports, this.Write);
            writer.Write(import.IsNonSharedInstanceRequired);
            this.Write(writer, import.Metadata);
            this.Write(writer, import.ExportFactory);
            this.Write(writer, import.ExportFactorySharingBoundaries, (w, v) => w.Write(v));
        }

        private RuntimeComposition.RuntimeImport ReadRuntimeImport(BinaryReader reader)
        {
            Trace("RuntimeImport", reader.BaseStream);

            byte kind = reader.ReadByte();
            MemberRef importingMember = default(MemberRef);
            ParameterRef importingParameter = default(ParameterRef);
            switch (kind)
            {
                case 1:
                    importingMember = this.ReadMemberRef(reader);
                    break;
                case 2:
                    importingParameter = this.ReadParameterRef(reader);
                    break;
                default:
                    throw new NotSupportedException();
            }

            var cardinality = this.ReadImportCardinality(reader);
            var satisfyingExports = this.ReadList(reader, this.ReadRuntimeExport);
            bool isNonSharedInstanceRequired = reader.ReadBoolean();
            var metadata = this.ReadMetadata(reader);
            var exportFactory = this.ReadTypeRef(reader);
            var exportFactorySharingBoundaries = this.ReadList(reader, r => r.ReadString());

            return importingMember.IsEmpty
                ? new RuntimeComposition.RuntimeImport(
                    importingParameter,
                    cardinality,
                    satisfyingExports,
                    isNonSharedInstanceRequired,
                    metadata,
                    exportFactory,
                    exportFactorySharingBoundaries)
                : new RuntimeComposition.RuntimeImport(
                    importingMember,
                    cardinality,
                    satisfyingExports,
                    isNonSharedInstanceRequired,
                    metadata,
                    exportFactory,
                    exportFactorySharingBoundaries);
        }

        private void Write(BinaryWriter writer, ConstructorRef constructorRef)
        {
            Trace("ConstructorRef", writer.BaseStream);

            this.Write(writer, constructorRef.DeclaringType);
            writer.Write(constructorRef.MetadataToken);
        }

        private ConstructorRef ReadConstructorRef(BinaryReader reader)
        {
            Trace("ConstructorRef", reader.BaseStream);

            var declaringType = this.ReadTypeRef(reader);
            var metadataToken = reader.ReadInt32();

            return new ConstructorRef(
                declaringType,
                metadataToken);
        }

        private void Write(BinaryWriter writer, TypeRef typeRef)
        {
            Trace("TypeRef", writer.BaseStream);

            if (typeRef == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                this.Write(writer, typeRef.AssemblyName);
                writer.Write(typeRef.MetadataToken);
                writer.Write((byte)typeRef.GenericTypeParameterCount);
                this.Write(writer, typeRef.GenericTypeArguments, this.Write);
            }
        }

        private TypeRef ReadTypeRef(BinaryReader reader)
        {
            Trace("TypeRef", reader.BaseStream);

            byte nullCheck = reader.ReadByte();
            if (nullCheck == 1)
            {
                var assemblyName = this.ReadAssemblyName(reader);
                var metadataToken = reader.ReadInt32();
                int genericTypeParameterCount = reader.ReadByte();
                var genericTypeArguments = this.ReadList(reader, this.ReadTypeRef);
                return TypeRef.Get(assemblyName, metadataToken, genericTypeParameterCount, genericTypeArguments.ToImmutableArray());
            }
            else
            {
                return default(TypeRef);
            }
        }

        private void Write(BinaryWriter writer, AssemblyName assemblyName)
        {
            Trace("AssemblyName", writer.BaseStream);

            writer.Write(assemblyName.FullName);
            writer.Write(assemblyName.CodeBase);
        }

        private AssemblyName ReadAssemblyName(BinaryReader reader)
        {
            Trace("AssemblyName", reader.BaseStream);

            string fullName = reader.ReadString();
            string codeBase = reader.ReadString();
            return new AssemblyName(fullName)
            {
                CodeBase = codeBase,
            };
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

        private void Write(BinaryWriter writer, string value)
        {
            Trace("String", writer.BaseStream);

            writer.Write(value != null ? (byte)1 : (byte)0);
            if (value != null)
            {
                writer.Write(value);
            }
        }

        private string ReadString(BinaryReader reader)
        {
            Trace("String", reader.BaseStream);

            byte nullCheck = reader.ReadByte();
            if (nullCheck == 1)
            {
                return reader.ReadString();
            }
            else
            {
                return null;
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

        private void Write(BinaryWriter writer, Array list, Action<BinaryWriter, object> itemWriter)
        {
            Trace((list != null ? list.GetType().GetElementType().Name : "null") + "[]", writer.BaseStream);

            if (list == null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(list.Length);
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

            if (count > 0xffff)
            {
                // Probably either file corruption or a bug in serialization.
                // Let's not take untold amounts of memory by throwing out suspiciously large lengths.
                throw new NotSupportedException();
            }

            var list = new T[count];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = itemReader(reader);
            }

            return list;
        }

        private Array ReadArray(BinaryReader reader, Func<BinaryReader, object> itemReader, Type elementType)
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

        private void Write(BinaryWriter writer, IReadOnlyDictionary<string, object> metadata)
        {
            Trace("Metadata", writer.BaseStream);

            writer.Write(metadata.Count);
            foreach (var entry in metadata)
            {
                writer.Write(entry.Key);

                // Special case values of type Type or Type[] to avoid defeating lazy load later.
                // We deserialize keeping the replaced TypeRef values so that they can be resolved
                // at the last possible moment by the metadata view at runtime.
                // Check out the ReadMetadata below, how it wraps the return value.
                if (entry.Value is Type)
                {
                    this.WriteObject(writer, TypeRef.Get((Type)entry.Value));
                }
                else if (entry.Value is Type[])
                {
                    this.WriteObject(writer, ((Type[])entry.Value).Select(TypeRef.Get).ToArray());
                }
                else
                {
                    this.WriteObject(writer, entry.Value);
                }
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

            return new LazyMetadataWrapper(metadata);
        }

        private void Write(BinaryWriter writer, ImportCardinality cardinality)
        {
            Trace("ImportCardinality", writer.BaseStream);

            writer.Write((byte)cardinality);
        }

        private ImportCardinality ReadImportCardinality(BinaryReader reader)
        {
            Trace("ImportCardinality", reader.BaseStream);
            return (ImportCardinality)reader.ReadByte();
        }

        private enum ObjectType : byte
        {
            Null,
            String,
            CreationPolicy,
            Type,
            Array,
            BinaryFormattedObject,
            TypeRef,
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
                    this.Write(writer, array, this.WriteObject);
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
                else if (typeof(TypeRef) == valueType)
                {
                    this.Write(writer, ObjectType.TypeRef);
                    this.Write(writer, (TypeRef)value);
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
                    return this.ReadArray(reader, this.ReadObject, elementType);
                case ObjectType.String:
                    return reader.ReadString();
                case ObjectType.CreationPolicy:
                    return (CreationPolicy)reader.ReadByte();
                case ObjectType.Type:
                    return this.ReadType(reader);
                case ObjectType.TypeRef:
                    return this.ReadTypeRef(reader);
                case ObjectType.BinaryFormattedObject:
                    var formatter = new BinaryFormatter();
                    return formatter.Deserialize(reader.BaseStream);
                default:
                    throw new NotSupportedException("Unsupported format: " + objectType);
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
