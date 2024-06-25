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
/// Provides formatters and recommended resolvers for the data types declared in this assembly.
/// </summary>
public class MessagePackSerializerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagePackSerializerContext"/> class.
    /// </summary>
    /// <param name="resolver">The assembly loader to use in the deserialized object tree.</param>
    public MessagePackSerializerContext(Resolver resolver)
    {
        this.Resolver = CompositeResolver.Create(
            new IMessagePackFormatter[]
            {
                new MetadataObjectFormatter(resolver),
                new MetadataDictionaryFormatter(resolver),
                new ImportMetadataViewConstraint.ImportMetadataViewConstraintFormatter(resolver),
                new TypeRef.TypeRefObjectFormatter(resolver),
                new RuntimeComposition.RuntimeCompositionFormatter(resolver),
                new RuntimeComposition.RuntimeImportFormatter(resolver),
                new ComposablePartDefinition.ComposablePartDefinitionFormatter(resolver),
                new ComposableCatalog.ComposableCatalogFormatter(resolver),
                AssemblyNameFormatter.Instance,
            });
        this.DefaultOptions = MessagePackSerializerOptions.Standard
            .WithResolver(new MyDedupingResolver(CompositeResolver.Create(
                this.Resolver,
                StandardResolverAllowPrivate.Instance)));
    }

    /// <summary>
    /// Gets a resolver that can be used to serialize and deserialize objects in this assembly.
    /// </summary>
    public IFormatterResolver Resolver { get; }

    /// <summary>
    /// Gets the recommended options to use when serializing and deserializing objects in this assembly.
    /// </summary>
    /// <remarks>
    /// This includes a resolver that combines <see cref="Resolver"/> with the <see cref="StandardResolverAllowPrivate"/> resolver
    /// and a de-duping resolver so that certain types only serialize once when they are by-value or by-reference equal with each other.
    /// </remarks>
    public MessagePackSerializerOptions DefaultOptions { get; }

    private class MyDedupingResolver : IFormatterResolver
    {
        private const sbyte ReferenceExtensionTypeCode = 1;
        private readonly Dictionary<Type, IMessagePackFormatter> dedupingFormatters = new();
        private readonly List<object?> deserializedObjects = new();
        private readonly IFormatterResolver inner;
        private readonly Dictionary<object, int> serializedObjects = new();
        private int serializingObjectCounter;

        internal MyDedupingResolver(IFormatterResolver inner)
        {
            this.inner = inner;
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
            private readonly MyDedupingResolver owner;

            internal DedupingFormatter(MyDedupingResolver owner)
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
