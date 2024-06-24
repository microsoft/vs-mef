// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.VisualStudio.Composition.Formatter;
using Microsoft.VisualStudio.Composition.Reflection;

/// <summary>
/// Provides a context for MessagePack serialization with additional options.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="MessagePackSerializerContext"/> class is used to configure the serialization and deserialization process in MessagePack.
/// It allows for customization of the serialization process by providing a resolver for formatters and a composition resolver.
/// </para>
/// <para>
/// An object of this class (or a derived class) is required to deserialize types declared within this assembly.
/// </para>
/// </remarks>
public class MessagePackSerializerContext : MessagePackSerializerOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagePackSerializerContext"/> class.
    /// </summary>
    /// <param name="resolver">The <see cref="Resolver"/> to use for loading assemblies at runtime.</param>
    public MessagePackSerializerContext(Resolver resolver)
        : this(resolver, StandardResolverAllowPrivate.Instance)
    {
        this.CompositionResolver = resolver;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagePackSerializerContext"/> class.
    /// </summary>
    /// <param name="compositionResolver">The <see cref="Resolver"/> to use for loading assemblies at runtime.</param>
    /// <param name="formatterResolver">
    /// The MessagePack object to use for resolving formatters during serialization.
    /// This is expected to be at least as capable as the default <see cref="StandardResolverAllowPrivate"/>.
    /// </param>
    private MessagePackSerializerContext(Resolver compositionResolver, IFormatterResolver formatterResolver)
        : base(Standard.WithResolver(GetIFormatterResolver(formatterResolver)))
    {
        this.CompositionResolver = compositionResolver;
    }

    public Resolver CompositionResolver { get; }

    private static IFormatterResolver GetIFormatterResolver(IFormatterResolver formatterResolver)
    {
        return new DedupingResolver(CompositeResolver.Create(
            new IMessagePackFormatter[]
            {
                AssemblyNameFormatter.Instance,
                MetadataDictionaryFormatter.Instance,
                MetadataObjectFormatter.Instance,
            },
            new IFormatterResolver[]
            {
                 formatterResolver,
            }));
    }

    private static readonly HashSet<Type> DedupingTypes =
    [
        typeof(string),
        typeof(RuntimeComposition.RuntimeExport),
        typeof(PropertyRef),
        typeof(FieldRef),
        typeof(ParameterRef),
        typeof(TypeRef),
        typeof(AssemblyName),
        typeof(StrongAssemblyIdentity),
    ];

    private class DedupingResolver : IFormatterResolver
    {
        private const sbyte ReferenceExtensionTypeCode = 1;
        private readonly Dictionary<Type, IMessagePackFormatter> dedupingFormatters = new();
        private readonly List<object?> deserializedObjects = new();
        private readonly IFormatterResolver inner;
        private readonly Dictionary<object, int> serializedObjects = new();
        private int serializingObjectCounter;

        internal DedupingResolver(IFormatterResolver inner)
        {
            this.inner = inner;
        }

        public IMessagePackFormatter<T>? GetFormatter<T>()
        {
            if (DedupingTypes.Contains(typeof(T)))
            {
                return this.GetDedupingFormatter<T>();
            }

            return this.inner.GetFormatter<T>();
        }

        private IMessagePackFormatter<T>? GetDedupingFormatter<T>()
        {
            if (!this.dedupingFormatters.TryGetValue(typeof(T), out IMessagePackFormatter? formatter))
            {
                formatter = new DedupingFormatter<T>(this);
                this.dedupingFormatters.Add(typeof(T), formatter);
            }

            return (IMessagePackFormatter<T>)formatter;
        }

        private class DedupingFormatter<T> : IMessagePackFormatter<T>
        {
            private readonly DedupingResolver owner;

            internal DedupingFormatter(DedupingResolver owner)
            {
                this.owner = owner;
            }

            public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (!typeof(T).IsValueType && reader.TryReadNil())
                {
                    return default!;
                }

                if (reader.NextMessagePackType == MessagePackType.Extension)
                {
                    MessagePackReader provisionaryReader = reader.CreatePeekReader();
                    ExtensionHeader extensionHeader = provisionaryReader.ReadExtensionFormatHeader();
                    if (extensionHeader.TypeCode == ReferenceExtensionTypeCode)
                    {
                        int id = provisionaryReader.ReadInt32();
                        reader = provisionaryReader;

                        return (T)(this.owner.deserializedObjects[id] ?? throw new MessagePackSerializationException("Unexpected null element in shared object array. Dependency cycle?"));
                    }
                }

                // Reserve our position in the array.
                int reservation = this.owner.deserializedObjects.Count;
                this.owner.deserializedObjects.Add(null);

                T value = this.owner.inner.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
                this.owner.deserializedObjects[reservation] = value;

                return value;
            }

            public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                if (this.owner.serializedObjects.TryGetValue(value, out int referenceId))
                {
                    // This object has already been written. Skip it this time.
                    int packLength = MessagePackWriter.GetEncodedLength(referenceId);
                    writer.WriteExtensionFormatHeader(new ExtensionHeader(ReferenceExtensionTypeCode, packLength));
                    writer.Write(referenceId);
                    return;
                }
                else
                {
                    this.owner.serializedObjects.Add(value, this.owner.serializingObjectCounter++);
                    this.owner.inner.GetFormatterWithVerify<T>().Serialize(ref writer, value, options);
                }
            }
        }
    }
}
