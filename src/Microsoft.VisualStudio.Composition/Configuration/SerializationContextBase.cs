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
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal abstract class SerializationContextBase
    {
        protected BinaryReader reader;

        protected BinaryWriter writer;

        protected Dictionary<object, uint> serializingObjectTable;

        protected Dictionary<uint, object> deserializingObjectTable;

        protected int indentationLevel;

#if TRACESTATS
        protected Dictionary<string, int> sizeStats;
#endif

        private readonly ImmutableDictionary<string, object>.Builder metadataBuilder = ImmutableDictionary.CreateBuilder<string, object>();

        private readonly byte[] guidBuffer = new byte[128 / 8];

        private long objectTableCapacityStreamPosition = -1; // -1 indicates the stream isn't capable of seeking.

        internal SerializationContextBase(BinaryReader reader, Resolver resolver)
        {
            Requires.NotNull(reader, nameof(reader));
            Requires.NotNull(resolver, nameof(resolver));

            this.reader = reader;
            this.Resolver = resolver;

            // At the head of the stream, read in the estimated or actual size of the object table we will require.
            // This reduces GC pressure and time spent resizing the object table during deserialization.
            int objectTableCapacity = reader.ReadInt32();
            int objectTableSafeCapacity = Math.Min(objectTableCapacity, 1000000); // protect against OOM in case of data corruption.
            this.deserializingObjectTable = new Dictionary<uint, object>(objectTableSafeCapacity);
        }

        internal SerializationContextBase(BinaryWriter writer, int estimatedObjectCount, Resolver resolver)
        {
            Requires.NotNull(writer, nameof(writer));
            Requires.NotNull(resolver, nameof(resolver));

            this.writer = writer;
            this.serializingObjectTable = new Dictionary<object, uint>(estimatedObjectCount, SmartInterningEqualityComparer.Default);
            this.Resolver = resolver;
#if TRACESTATS
            this.sizeStats = new Dictionary<string, int>();
#endif

            // Don't use compressed uint here. It must be a fixed size because we *may*
            // come back and rewrite this at the end of serialization if this stream is seekable.
            // Otherwise, we'll leave it at our best estimate given the size of the data being serialized.
            Stream writerStream = writer.BaseStream;
            this.objectTableCapacityStreamPosition = writerStream.CanSeek ? writer.BaseStream.Position : -1;
            this.writer.Write(estimatedObjectCount);
        }

        [Flags]
        private enum TypeRefFlags : byte
        {
            None = 0x0,
            IsArray = 0x1,
        }

        protected enum ObjectType : byte
        {
            Null,
            String,
            CreationPolicy,
            Type,
            Array,
            BinaryFormattedObject,
            TypeRef,
            BoolTrue,
            BoolFalse,
            Int32,
            Char,
            Guid,
            Enum32Substitution,
            TypeSubstitution,
            TypeArraySubstitution,
            Single,
            Double,
            UInt16,
            Int64,
            UInt64,
            Int16,
            UInt32,
            Byte,
            SByte,
        }

        /// <summary>
        /// Gets the resolver to use when deserializing.
        /// </summary>
        protected Resolver Resolver { get; }

        protected internal void FinalizeObjectTableCapacity()
        {
            Verify.Operation(this.writer != null, Strings.OnlySupportedOnWriteOperations);

            // For efficiency in deserialization, go back and write the actual number of objects
            // in the object table so the deserializer can allocate space up front and avoid dictionary resizing
            // which can otherwise produce a *lot* of garbage.
            // We can only do this on streams that support seeking.
            if (this.objectTableCapacityStreamPosition >= 0)
            {
                // Always flush the writer before repositioning the stream to avoid corrupting data.
                this.writer.Flush();
                Stream writerStream = this.writer.BaseStream;

                // Reposition the stream to the point at which we wrote out our estimated required capacity.
                long tailPosition = writerStream.Position;
                writerStream.Position = this.objectTableCapacityStreamPosition;

                // Overwrite the estimate with the actual size.
                this.writer.Write(this.serializingObjectTable.Count);

                // Reposition the stream back to the end of our own serialization.
                this.writer.Flush();
                writerStream.Position = tailPosition;
            }
        }

        protected SerializationTrace Trace(string elementName, bool isArray = false)
        {
            Stream stream = null;
#if TRACESERIALIZATION || TRACESTATS
            // It turns out that acquiring the stream is very expensive because
            // each time you get it, the writer is flushed. Since we use the stream
            // for its Position, flushing is actually important. But it's very slow,
            // so don't do it in production.
            stream = reader != null ? reader.BaseStream : writer.BaseStream;
#endif

            return new SerializationTrace(this, elementName, isArray, stream);
        }

        protected void Write(MethodRef methodRef)
        {
            using (this.Trace("MethodRef"))
            {
                if (methodRef.IsEmpty)
                {
                    this.writer.Write((byte)0);
                }
                else
                {
                    this.writer.Write((byte)1);
                    this.Write(methodRef.DeclaringType);
                    this.WriteCompressedMetadataToken(methodRef.MetadataToken, MetadataTokenType.Method);
                    this.Write(methodRef.Name);
                    this.Write(methodRef.ParameterTypes, this.Write);
                    this.Write(methodRef.GenericMethodArguments, this.Write);
                }
            }
        }

        protected MethodRef ReadMethodRef()
        {
            using (this.Trace("MethodRef"))
            {
                byte nullCheck = this.reader.ReadByte();
                if (nullCheck == 1)
                {
                    var declaringType = this.ReadTypeRef();
                    var metadataToken = this.ReadCompressedMetadataToken(MetadataTokenType.Method);
                    var name = this.ReadString();
                    var parameterTypes = this.ReadList(this.reader, this.ReadTypeRef).ToImmutableArray();
                    var genericMethodArguments = this.ReadList(this.reader, this.ReadTypeRef).ToImmutableArray();
                    return new MethodRef(declaringType, metadataToken, name, parameterTypes, genericMethodArguments);
                }
                else
                {
                    return default(MethodRef);
                }
            }
        }

        protected void Write(MemberRef memberRef)
        {
            using (this.Trace("MemberRef"))
            {
                if (memberRef.IsConstructor)
                {
                    this.writer.Write((byte)1);
                    this.Write(memberRef.Constructor);
                }
                else if (memberRef.IsField)
                {
                    this.writer.Write((byte)2);
                    this.Write(memberRef.Field);
                }
                else if (memberRef.IsProperty)
                {
                    this.writer.Write((byte)3);
                    this.Write(memberRef.Property);
                }
                else if (memberRef.IsMethod)
                {
                    this.writer.Write((byte)4);
                    this.Write(memberRef.Method);
                }
                else
                {
                    this.writer.Write((byte)0);
                }
            }
        }

        protected MemberRef ReadMemberRef()
        {
            using (this.Trace("MemberRef"))
            {
                int kind = this.reader.ReadByte();
                switch (kind)
                {
                    case 0:
                        return default(MemberRef);
                    case 1:
                        return new MemberRef(this.ReadConstructorRef());
                    case 2:
                        return new MemberRef(this.ReadFieldRef());
                    case 3:
                        return new MemberRef(this.ReadPropertyRef());
                    case 4:
                        return new MemberRef(this.ReadMethodRef());
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        protected void Write(PropertyRef propertyRef)
        {
            using (this.Trace("PropertyRef"))
            {
                this.Write(propertyRef.DeclaringType);
                this.WriteCompressedMetadataToken(propertyRef.MetadataToken, MetadataTokenType.Property);
                this.Write(propertyRef.Name);

                byte flags = 0;
                flags |= propertyRef.GetMethodMetadataToken.HasValue ? (byte)0x1 : (byte)0x0;
                flags |= propertyRef.SetMethodMetadataToken.HasValue ? (byte)0x2 : (byte)0x0;
                this.writer.Write(flags);

                if (propertyRef.GetMethodMetadataToken.HasValue)
                {
                    this.WriteCompressedMetadataToken(propertyRef.GetMethodMetadataToken.Value, MetadataTokenType.Method);
                }

                if (propertyRef.SetMethodMetadataToken.HasValue)
                {
                    this.WriteCompressedMetadataToken(propertyRef.SetMethodMetadataToken.Value, MetadataTokenType.Method);
                }
            }
        }

        protected PropertyRef ReadPropertyRef()
        {
            using (this.Trace("PropertyRef"))
            {
                var declaringType = this.ReadTypeRef();
                var metadataToken = this.ReadCompressedMetadataToken(MetadataTokenType.Property);
                var name = this.ReadString();

                byte flags = this.reader.ReadByte();
                int? getter = null, setter = null;
                if ((flags & 0x1) != 0)
                {
                    getter = this.ReadCompressedMetadataToken(MetadataTokenType.Method);
                }

                if ((flags & 0x2) != 0)
                {
                    setter = this.ReadCompressedMetadataToken(MetadataTokenType.Method);
                }

                return new PropertyRef(
                    declaringType,
                    metadataToken,
                    getter,
                    setter,
                    name);
            }
        }

        protected void Write(FieldRef fieldRef)
        {
            using (this.Trace("FieldRef"))
            {
                this.writer.Write(!fieldRef.IsEmpty);
                if (!fieldRef.IsEmpty)
                {
                    this.Write(fieldRef.DeclaringType);
                    this.WriteCompressedMetadataToken(fieldRef.MetadataToken, MetadataTokenType.Field);
                    this.Write(fieldRef.Name);
                }
            }
        }

        protected FieldRef ReadFieldRef()
        {
            using (this.Trace("FieldRef"))
            {
                if (this.reader.ReadBoolean())
                {
                    var declaringType = this.ReadTypeRef();
                    int metadataToken = this.ReadCompressedMetadataToken(MetadataTokenType.Field);
                    var name = this.ReadString();
                    return new FieldRef(declaringType, metadataToken, name);
                }
                else
                {
                    return default(FieldRef);
                }
            }
        }

        protected void Write(ParameterRef parameterRef)
        {
            using (this.Trace("ParameterRef"))
            {
                this.writer.Write(!parameterRef.IsEmpty);
                if (!parameterRef.IsEmpty)
                {
                    this.Write(parameterRef.Constructor);
                    this.Write(parameterRef.Method);
                    this.writer.Write((byte)parameterRef.ParameterIndex);
                }
            }
        }

        protected ParameterRef ReadParameterRef()
        {
            using (this.Trace("ParameterRef"))
            {
                if (this.reader.ReadBoolean())
                {
                    var ctor = this.ReadConstructorRef();
                    var method = this.ReadMethodRef();
                    var parameterIndex = this.reader.ReadByte();
                    return ctor.IsEmpty ? new ParameterRef(method, parameterIndex) : new ParameterRef(ctor, parameterIndex);
                }
                else
                {
                    return default(ParameterRef);
                }
            }
        }

        protected void WriteCompressedMetadataToken(int metadataToken, MetadataTokenType type)
        {
            uint token = (uint)metadataToken;
            uint flags = (uint)type;
            Requires.Argument((token & (uint)MetadataTokenType.Mask) == flags, "type", Strings.WrongType); // just a sanity check
            this.WriteCompressedUInt(token & ~(uint)MetadataTokenType.Mask);
        }

        protected int ReadCompressedMetadataToken(MetadataTokenType type)
        {
            return (int)(this.ReadCompressedUInt() | (uint)type);
        }

        protected void Write(ConstructorRef constructorRef)
        {
            Requires.Argument(!constructorRef.IsEmpty, "constructorRef", Strings.CannotBeEmpty);
            using (this.Trace("ConstructorRef"))
            {
                this.Write(constructorRef.DeclaringType);
                this.WriteCompressedMetadataToken(constructorRef.MetadataToken, MetadataTokenType.Method);
                this.Write(constructorRef.ParameterTypes, this.Write);
            }
        }

        protected ConstructorRef ReadConstructorRef()
        {
            using (this.Trace("ConstructorRef"))
            {
                var declaringType = this.ReadTypeRef();
                var metadataToken = this.ReadCompressedMetadataToken(MetadataTokenType.Method);
                var argumentTypes = this.ReadList(this.reader, this.ReadTypeRef).ToImmutableArray();

                return new ConstructorRef(
                    declaringType,
                    metadataToken,
                    argumentTypes);
            }
        }

        protected void Write(TypeRef typeRef)
        {
            using (this.Trace("TypeRef"))
            {
                if (this.TryPrepareSerializeReusableObject(typeRef))
                {
                    this.Write(typeRef.AssemblyId);
                    this.WriteCompressedMetadataToken(typeRef.MetadataToken, MetadataTokenType.Type);
                    this.Write(typeRef.FullName);

                    var flags = TypeRefFlags.None;
                    flags |= typeRef.IsArray ? TypeRefFlags.IsArray : TypeRefFlags.None;
                    this.writer.Write((byte)flags);

                    this.WriteCompressedUInt((uint)typeRef.GenericTypeParameterCount);
                    this.Write(typeRef.GenericTypeArguments, this.Write);
                }
            }
        }

        protected TypeRef ReadTypeRef()
        {
            using (this.Trace("TypeRef"))
            {
                uint id;
                TypeRef value;
                if (this.TryPrepareDeserializeReusableObject(out id, out value))
                {
                    var assemblyId = this.ReadStrongAssemblyIdentity();
                    var metadataToken = this.ReadCompressedMetadataToken(MetadataTokenType.Type);
                    var fullName = this.ReadString();
                    var flags = (TypeRefFlags)this.reader.ReadByte();
                    int genericTypeParameterCount = (int)this.ReadCompressedUInt();
                    var genericTypeArguments = this.ReadList(this.reader, this.ReadTypeRef).ToImmutableArray();
                    value = TypeRef.Get(this.Resolver, assemblyId, metadataToken, fullName, flags.HasFlag(TypeRefFlags.IsArray), genericTypeParameterCount, genericTypeArguments);

                    this.OnDeserializedReusableObject(id, value);
                }

                return value;
            }
        }

        protected void Write(AssemblyName assemblyName)
        {
            using (this.Trace("AssemblyName"))
            {
                if (this.TryPrepareSerializeReusableObject(assemblyName))
                {
                    this.Write(assemblyName.FullName);
#if DESKTOP
                    this.Write(assemblyName.CodeBase);
#else
                    this.Write((string)null); // keep the binary format consistent even if we can't write this.
#endif
                }
            }
        }

        protected AssemblyName ReadAssemblyName()
        {
            using (this.Trace("AssemblyName"))
            {
                uint id;
                AssemblyName value;
                if (this.TryPrepareDeserializeReusableObject(out id, out value))
                {
                    string fullName = this.ReadString();
                    string codeBase = this.ReadString();
                    value = new AssemblyName(fullName);
#if DESKTOP
                    value.CodeBase = codeBase;
#endif
                    this.OnDeserializedReusableObject(id, value);
                }

                return value;
            }
        }

        protected void Write(StrongAssemblyIdentity assemblyMetadata)
        {
            using (this.Trace(nameof(StrongAssemblyIdentity)))
            {
                if (this.TryPrepareSerializeReusableObject(assemblyMetadata))
                {
                    this.Write(assemblyMetadata.Name);
                    this.Write(assemblyMetadata.Mvid);
                }
            }
        }

        protected StrongAssemblyIdentity ReadStrongAssemblyIdentity()
        {
            using (this.Trace(nameof(StrongAssemblyIdentity)))
            {
                if (this.TryPrepareDeserializeReusableObject(out uint id, out StrongAssemblyIdentity value))
                {
                    AssemblyName name = this.ReadAssemblyName();
                    Guid mvid = this.ReadGuid();
                    value = new StrongAssemblyIdentity(name, mvid);

                    this.OnDeserializedReusableObject(id, value);
                }

                return value;
            }
        }

        protected void Write(DateTime value)
        {
            using (this.Trace(nameof(DateTime)))
            {
                this.writer.Write(value.Ticks);
            }
        }

        protected DateTime ReadDateTime()
        {
            using (this.Trace(nameof(DateTime)))
            {
                return new DateTime(this.reader.ReadInt64());
            }
        }

        protected void Write(Guid value)
        {
            using (this.Trace(nameof(Guid)))
            {
                this.writer.Write(value.ToByteArray());
            }
        }

        protected Guid ReadGuid()
        {
            using (this.Trace(nameof(Guid)))
            {
                this.ReadBuffer(this.guidBuffer, 0, this.guidBuffer.Length);
                return new Guid(this.guidBuffer);
            }
        }

        protected void Write(string value)
        {
            using (this.Trace("String"))
            {
                if (this.TryPrepareSerializeReusableObject(value))
                {
                    this.writer.Write(value);
                }
            }
        }

        protected string ReadString()
        {
            using (this.Trace("String"))
            {
                uint id;
                string value;
                if (this.TryPrepareDeserializeReusableObject(out id, out value))
                {
                    value = this.reader.ReadString();
                    this.OnDeserializedReusableObject(id, value);
                }

                return value;
            }
        }

        protected void WriteCompressedUInt(uint value)
        {
            CompressedUInt.WriteCompressedUInt(this.writer, value);
        }

        protected uint ReadCompressedUInt()
        {
            return CompressedUInt.ReadCompressedUInt(this.reader);
        }

        protected void Write<T>(IReadOnlyCollection<T> list, Action<T> itemWriter)
        {
            Requires.NotNull(list, nameof(list));
            using (this.Trace("List<" + typeof(T).Name + ">"))
            {
                this.WriteCompressedUInt((uint)list.Count);
                foreach (var item in list)
                {
                    itemWriter(item);
                }
            }
        }

        protected void Write(Array list, Action<object> itemWriter)
        {
            Requires.NotNull(list, nameof(list));
            using (this.Trace((list != null ? list.GetType().GetElementType().Name : "null") + "[]"))
            {
                this.WriteCompressedUInt((uint)list.Length);
                foreach (var item in list)
                {
                    itemWriter(item);
                }
            }
        }

        protected IReadOnlyList<T> ReadList<T>(Func<T> itemReader)
        {
            return this.ReadList<T>(this.reader, itemReader);
        }

        protected IReadOnlyList<T> ReadList<T>(BinaryReader reader, Func<T> itemReader)
        {
            using (this.Trace(typeof(T).Name, isArray: true))
            {
                uint count = this.ReadCompressedUInt();
                if (count > 0xffff)
                {
                    // Probably either file corruption or a bug in serialization.
                    // Let's not take untold amounts of memory by throwing out suspiciously large lengths.
                    throw new NotSupportedException();
                }

                var list = new T[count];
                for (int i = 0; i < list.Length; i++)
                {
                    list[i] = itemReader();
                }

                return list;
            }
        }

        protected Array ReadArray(BinaryReader reader, Func<object> itemReader, Type elementType)
        {
            using (this.Trace(elementType.Name, isArray: true))
            {
                uint count = this.ReadCompressedUInt();
                if (count > 0xffff)
                {
                    // Probably either file corruption or a bug in serialization.
                    // Let's not take untold amounts of memory by throwing out suspiciously large lengths.
                    throw new NotSupportedException();
                }

                var list = Array.CreateInstance(elementType, (int)count);
                for (int i = 0; i < list.Length; i++)
                {
                    object value = itemReader();
                    list.SetValue(value, i);
                }

                return list;
            }
        }

        /// <summary>
        /// Reads the specified number of bytes into a buffer.
        /// This method will not return till exactly the requested number of bytes are read.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="start">The starting position in the buffer to write to.</param>
        /// <param name="count">The number of bytes to read.</param>
        protected void ReadBuffer(byte[] buffer, int start, int count)
        {
            // Streams and BinaryReader reserve the right to read fewer bytes than requested.
            // So we will keep asking until it reaches the end of the stream or we get what we need.
            while (count > 0)
            {
                int bytesRead = this.reader.Read(buffer, start, count);
                if (bytesRead == 0)
                {
                    // Premature end of stream.
                    throw new NotSupportedException();
                }

                start += bytesRead;
                count -= bytesRead;
            }
        }

        protected void Write(IReadOnlyDictionary<string, object> metadata)
        {
            using (this.Trace("Metadata"))
            {
                this.WriteCompressedUInt((uint)metadata.Count);

                // Special case certain values to avoid defeating lazy load later.
                // Check out the ReadMetadata below, how it wraps the return value.
                var serializedMetadata = new LazyMetadataWrapper(metadata.ToImmutableDictionary(), LazyMetadataWrapper.Direction.ToSubstitutedValue, this.Resolver);
                foreach (var entry in serializedMetadata)
                {
                    this.Write(entry.Key);
                    this.WriteObject(entry.Value);
                }
            }
        }

        protected IReadOnlyDictionary<string, object> ReadMetadata()
        {
            using (this.Trace("Metadata"))
            {
                // PERF TIP: if ReadMetadata shows up on startup perf traces,
                // we could simply read the blob containing the metadata into a byte[]
                // and defer actually deserializing it until such time as the metadata
                // is actually required.
                // We might do this with minimal impact to other code by implementing
                // IReadOnlyDictionary<string, object> ourselves such that on the first
                // access of any of its contents, we'll do a just-in-time deserialization,
                // and perhaps only of the requested values.
                uint count = this.ReadCompressedUInt();
                var metadata = ImmutableDictionary<string, object>.Empty;

                if (count > 0)
                {
                    var builder = this.metadataBuilder; // reuse builder to save on GC pressure
                    for (int i = 0; i < count; i++)
                    {
                        string key = this.ReadString();
                        object value = this.ReadObject();
                        builder.Add(key, value);
                    }

                    metadata = builder.ToImmutable();
                    builder.Clear(); // clean up for the next user.
                }

                return new LazyMetadataWrapper(metadata, LazyMetadataWrapper.Direction.ToOriginalValue, this.Resolver);
            }
        }

        protected void Write(ImportCardinality cardinality)
        {
            using (this.Trace("ImportCardinality"))
            {
                this.writer.Write((byte)cardinality);
            }
        }

        protected ImportCardinality ReadImportCardinality()
        {
            using (this.Trace("ImportCardinality"))
            {
                return (ImportCardinality)this.reader.ReadByte();
            }
        }

        /// <summary>
        /// Prepares the object for referential sharing in the serialization stream.
        /// </summary>
        /// <param name="value">The value that may be serialized more than once.</param>
        /// <returns><c>true</c> if the object should be serialized; otherwise <c>false</c>.</returns>
        protected bool TryPrepareSerializeReusableObject(object value)
        {
            uint id;
            bool result;
            if (value == null)
            {
                id = 0;
                result = false;
            }
            else if (this.serializingObjectTable.TryGetValue(value, out id))
            {
                // The object has already been serialized.
                result = false;
            }
            else
            {
                this.serializingObjectTable.Add(value, id = (uint)this.serializingObjectTable.Count + 1);
                result = true;
            }

            this.WriteCompressedUInt(id);
            return result;
        }

        /// <summary>
        /// Gets an object that has already been deserialized, if available.
        /// </summary>
        /// <typeparam name="T">The type of deserialized object to retrieve.</typeparam>
        /// <param name="id">Receives the ID of the object.</param>
        /// <param name="value">Receives the value of the object, if available.</param>
        /// <returns><c>true</c> if the caller should deserialize the object; <c>false</c> if the object is in <paramref name="value"/>.</returns>
        protected bool TryPrepareDeserializeReusableObject<T>(out uint id, out T value)
            where T : class
        {
            id = this.ReadCompressedUInt();
            if (id == 0)
            {
                value = null;
                return false;
            }

            object valueObject;
            bool result = !this.deserializingObjectTable.TryGetValue(id, out valueObject);
            value = (T)valueObject;
            return result;
        }

        protected void OnDeserializedReusableObject(uint id, object value)
        {
            this.deserializingObjectTable.Add(id, value);
        }

        protected void WriteObject(object value)
        {
            if (value == null)
            {
                using (this.Trace("Object (null)"))
                {
                    this.Write(ObjectType.Null);
                }
            }
            else
            {
                Type valueType = value.GetType();
                using (this.Trace("Object (" + valueType.Name + ")"))
                {
                    if (valueType.IsArray)
                    {
                        Array array = (Array)value;
                        this.Write(ObjectType.Array);
                        TypeRef elementTypeRef = TypeRef.Get(valueType.GetElementType(), this.Resolver);
                        this.Write(elementTypeRef);
                        this.Write(array, this.WriteObject);
                    }
                    else if (valueType == typeof(bool))
                    {
                        this.Write((bool)value ? ObjectType.BoolTrue : ObjectType.BoolFalse);
                    }
                    else if (valueType == typeof(string))
                    {
                        this.Write(ObjectType.String);
                        this.Write((string)value);
                    }
                    else if (valueType == typeof(long))
                    {
                        this.Write(ObjectType.Int64);
                        this.writer.Write((long)value);
                    }
                    else if (valueType == typeof(ulong))
                    {
                        this.Write(ObjectType.UInt64);
                        this.writer.Write((ulong)value);
                    }
                    else if (valueType == typeof(int))
                    {
                        this.Write(ObjectType.Int32);
                        this.writer.Write((int)value);
                    }
                    else if (valueType == typeof(uint))
                    {
                        this.Write(ObjectType.UInt32);
                        this.writer.Write((uint)value);
                    }
                    else if (valueType == typeof(short))
                    {
                        this.Write(ObjectType.Int16);
                        this.writer.Write((short)value);
                    }
                    else if (valueType == typeof(ushort))
                    {
                        this.Write(ObjectType.UInt16);
                        this.writer.Write((ushort)value);
                    }
                    else if (valueType == typeof(byte))
                    {
                        this.Write(ObjectType.Byte);
                        this.writer.Write((byte)value);
                    }
                    else if (valueType == typeof(sbyte))
                    {
                        this.Write(ObjectType.SByte);
                        this.writer.Write((sbyte)value);
                    }
                    else if (valueType == typeof(float))
                    {
                        this.Write(ObjectType.Single);
                        this.writer.Write((float)value);
                    }
                    else if (valueType == typeof(double))
                    {
                        this.Write(ObjectType.Double);
                        this.writer.Write((double)value);
                    }
                    else if (valueType == typeof(char))
                    {
                        this.Write(ObjectType.Char);
                        this.writer.Write((char)value);
                    }
                    else if (valueType == typeof(Guid))
                    {
                        this.Write(ObjectType.Guid);
                        this.Write((Guid)value);
                    }
                    else if (valueType == typeof(CreationPolicy)) // TODO: how do we handle arbitrary value types?
                    {
                        this.Write(ObjectType.CreationPolicy);
                        this.writer.Write((byte)(CreationPolicy)value);
                    }
                    else if (typeof(Type).GetTypeInfo().IsAssignableFrom(valueType))
                    {
                        this.Write(ObjectType.Type);
                        this.Write(TypeRef.Get((Type)value, this.Resolver));
                    }
                    else if (typeof(TypeRef) == valueType)
                    {
                        this.Write(ObjectType.TypeRef);
                        this.Write((TypeRef)value);
                    }
                    else if (typeof(LazyMetadataWrapper.Enum32Substitution) == valueType)
                    {
                        var substValue = (LazyMetadataWrapper.Enum32Substitution)value;
                        this.Write(ObjectType.Enum32Substitution);
                        this.Write(substValue.EnumType);
                        this.writer.Write(substValue.RawValue);
                    }
                    else if (typeof(LazyMetadataWrapper.TypeSubstitution) == valueType)
                    {
                        var substValue = (LazyMetadataWrapper.TypeSubstitution)value;
                        this.Write(ObjectType.TypeSubstitution);
                        this.Write(substValue.TypeRef);
                    }
                    else if (typeof(LazyMetadataWrapper.TypeArraySubstitution) == valueType)
                    {
                        var substValue = (LazyMetadataWrapper.TypeArraySubstitution)value;
                        this.Write(ObjectType.TypeArraySubstitution);
                        this.Write(substValue.TypeRefArray, this.Write);
                    }
                    else
                    {
#if DESKTOP
                        Debug.WriteLine("Falling back to binary formatter for value of type: {0}", valueType);
                        this.Write(ObjectType.BinaryFormattedObject);
                        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        this.writer.Flush();
                        formatter.Serialize(this.writer.BaseStream, value);
#else
                        throw new NotSupportedException("Object of type " + valueType + " cannot be serialized on this platform.");
#endif
                    }
                }
            }
        }

        protected object ReadObject()
        {
            using (this.Trace("Object"))
            {
                ObjectType objectType = this.ReadObjectType();
                switch (objectType)
                {
                    case ObjectType.Null:
                        return null;
                    case ObjectType.Array:
                        Type elementType = this.ReadTypeRef().Resolve();
                        return this.ReadArray(this.reader, this.ReadObject, elementType);
                    case ObjectType.BoolTrue:
                        return true;
                    case ObjectType.BoolFalse:
                        return false;
                    case ObjectType.Int64:
                        return this.reader.ReadInt64();
                    case ObjectType.UInt64:
                        return this.reader.ReadUInt64();
                    case ObjectType.Int32:
                        return this.reader.ReadInt32();
                    case ObjectType.UInt32:
                        return this.reader.ReadUInt32();
                    case ObjectType.Int16:
                        return this.reader.ReadInt16();
                    case ObjectType.UInt16:
                        return this.reader.ReadUInt16();
                    case ObjectType.Byte:
                        return this.reader.ReadByte();
                    case ObjectType.SByte:
                        return this.reader.ReadSByte();
                    case ObjectType.Single:
                        return this.reader.ReadSingle();
                    case ObjectType.Double:
                        return this.reader.ReadDouble();
                    case ObjectType.String:
                        return this.ReadString();
                    case ObjectType.Char:
                        return this.reader.ReadChar();
                    case ObjectType.Guid:
                        return this.ReadGuid();
                    case ObjectType.CreationPolicy:
                        return (CreationPolicy)this.reader.ReadByte();
                    case ObjectType.Type:
                        return this.ReadTypeRef().Resolve();
                    case ObjectType.TypeRef:
                        return this.ReadTypeRef();
                    case ObjectType.Enum32Substitution:
                        TypeRef enumType = this.ReadTypeRef();
                        int rawValue = this.reader.ReadInt32();
                        return new LazyMetadataWrapper.Enum32Substitution(enumType, rawValue);
                    case ObjectType.TypeSubstitution:
                        TypeRef typeRef = this.ReadTypeRef();
                        return new LazyMetadataWrapper.TypeSubstitution(typeRef);
                    case ObjectType.TypeArraySubstitution:
                        IReadOnlyList<TypeRef> typeRefArray = this.ReadList(this.reader, this.ReadTypeRef);
                        return new LazyMetadataWrapper.TypeArraySubstitution(typeRefArray, this.Resolver);
                    case ObjectType.BinaryFormattedObject:
#if DESKTOP
                        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        return formatter.Deserialize(this.reader.BaseStream);
#else
                        throw new NotSupportedException("BinaryFormatter object cannot be deserialized on this platform.");
#endif
                    default:
                        throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedFormat, objectType));
                }
            }
        }

        protected void Write(ObjectType type)
        {
            this.writer.Write((byte)type);
        }

        protected ObjectType ReadObjectType()
        {
            var objectType = (ObjectType)this.reader.ReadByte();
            return objectType;
        }

        [Conditional("TRACESTATS")]
        protected void TraceStats()
        {
#if TRACESTATS
            if (this.sizeStats != null)
            {
                foreach (var item in this.sizeStats.OrderByDescending(kv => kv.Value))
                {
                    Debug.WriteLine("{0,7} {1}", item.Value, item.Key);
                }
            }
#endif
        }

        protected struct SerializationTrace : IDisposable
        {
            private const string Indent = "  ";
            private readonly SerializationContextBase context;
            private readonly string elementName;
            private readonly bool isArray;
            private readonly Stream stream;
            private readonly int startStreamPosition;

            internal SerializationTrace(SerializationContextBase context, string elementName, bool isArray, Stream stream)
            {
                this.context = context;
                this.elementName = elementName;
                this.isArray = isArray;
                this.stream = stream;

                this.context.indentationLevel++;
                this.startStreamPosition = stream != null ? (int)stream.Position : 0;

#if DEBUG && TRACESERIALIZATION
                    for (int i = 0; i < this.context.indentationLevel; i++)
                    {
                        Debug.Write(Indent);
                    }

                    Debug.WriteLine("Serialization: {2,7} {0}{1}", elementName, isArray ? "[]" : string.Empty, stream.Position);
#endif
            }

            public void Dispose()
            {
                this.context.indentationLevel--;

                if (this.stream != null)
                {
#if TRACESTATS
                    if (this.context.sizeStats != null)
                    {
                        int length = (int)this.stream.Position - this.startStreamPosition;
                        string elementNameWitharray = this.isArray ? (this.elementName + "[]") : this.elementName;
                        this.context.sizeStats[elementNameWitharray] = this.context.sizeStats.GetValueOrDefault(elementNameWitharray) + length;
                    }
#endif
                }
            }
        }

        /// <summary>
        /// An equality comparer that provides a bit better recognition of objects for better interning.
        /// </summary>
        private class SmartInterningEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly IEqualityComparer<object> Default = new SmartInterningEqualityComparer();

            private static readonly IEqualityComparer<object> Fallback = EqualityComparer<object>.Default;

            private SmartInterningEqualityComparer()
            {
            }

            public new bool Equals(object x, object y)
            {
                if (x is AssemblyName && y is AssemblyName)
                {
                    return ByValueEquality.AssemblyName.Equals((AssemblyName)x, (AssemblyName)y);
                }

                return Fallback.Equals(x, y);
            }

            public int GetHashCode(object obj)
            {
                if (obj is AssemblyName)
                {
                    return ByValueEquality.AssemblyName.GetHashCode((AssemblyName)obj);
                }

                return Fallback.GetHashCode(obj);
            }
        }
    }
}
