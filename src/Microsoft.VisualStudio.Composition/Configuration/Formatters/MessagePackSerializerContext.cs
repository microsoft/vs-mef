// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using System.Runtime.Serialization;
    using MessagePack;
    using MessagePack.Formatters;
    using MessagePack.Resolvers;
    using Microsoft.VisualStudio.Composition.Formatter;

    internal class MessagePackSerializerContext : MessagePackSerializerOptions, IDisposable
    {
        public MessagePackSerializerContext(IFormatterResolver resolver, Resolver compositionResolver)
            : base(GetIFormatterResolver(resolver))
        {
            this.CompositionResolver = compositionResolver;
        }

        public Resolver CompositionResolver { get; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static IFormatterResolver GetIFormatterResolver(IFormatterResolver resolver) =>
            CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                    //new IgnoreFormatter<CancellationToken>(),
                    //new IgnoreFormatter<Task>(),
                    ComposableCatalogFormatter.Instance,
                    ComposablePartDefinitionFormatter.Instance,
                    ExportDefinitionFormatter.Instance,
                    FieldRefFormatter.Instance,
                    ImportDefinitionBindingFormatter.Instance,
                    ImportDefinitionFormatter.Instance,
                    ImportMetadataViewConstraintFormatter.Instance,
                    ExportMetadataValueImportConstraintFormatter.Instance,
                    ExportTypeIdentityConstraintFormatter.Instance,
                    MetadataDictionaryFormatter.Instance,
                    MethodRefFormatter.Instance,
                    ParameterRefFormatter.Instance,
                    PartCreationPolicyConstraintFormatter.Instance,
                    PropertyRefFormatter.Instance,
                    RuntimeCompositionFormatter.Instance,
                    RuntimeExportFormatter.Instance,
                    RuntimeImportFormatter.Instance,
                    RuntimePartFormatter.Instance,
                    new StringInterningFormatter(),
                },
                new IFormatterResolver[]
                {
                     new DedupingResolver(resolver),
                });

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
                if (!typeof(T).IsValueType)
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
}
