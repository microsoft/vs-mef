﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;

internal class MetadataDictionaryFormatter(Resolver compositionResolver) : IMessagePackFormatter<IReadOnlyDictionary<string, object?>>
{
    private readonly MetadataObjectFormatter metadataObjectFormatter = new(compositionResolver);

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, IReadOnlyDictionary<string, object?> value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteMapHeader(value.Count);

        // Special case certain values to avoid defeating lazy load later. Check out the
        // ReadMetadata below, how it wraps the return value.
        IReadOnlyDictionary<string, object?> serializedMetadata;

        // Unwrap the metadata if its an instance of LazyMetaDataWrapper, the wrapper may end up
        // implicitly resolving TypeRefs to Types which is undesirable.
        value = LazyMetadataWrapper.TryUnwrap(value);
        serializedMetadata = new LazyMetadataWrapper(value.ToImmutableDictionary(), LazyMetadataWrapper.Direction.ToSubstitutedValue, compositionResolver);

        var stringFormatter = options.Resolver.GetFormatterWithVerify<string>();

        foreach (KeyValuePair<string, object?> item in serializedMetadata)
        {
            stringFormatter.Serialize(ref writer, item.Key, options);
            this.metadataObjectFormatter.Serialize(ref writer, item.Value, options);
        }
    }

    public IReadOnlyDictionary<string, object?> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        ImmutableDictionary<string, object?> metadata = ImmutableDictionary<string, object?>.Empty;

        if (reader.TryReadNil())
        {
            return metadata;
        }

        int count = reader.ReadMapHeader();
        var stringFormatter = options.Resolver.GetFormatterWithVerify<string>();

        if (count > 0)
        {
            ImmutableDictionary<string, object?>.Builder builder = ImmutableDictionary.CreateBuilder<string, object?>();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    string? key = stringFormatter.Deserialize(ref reader, options);
                    object? value = this.metadataObjectFormatter.Deserialize(ref reader, options);

                    builder.Add(key, value);
                }
            }
            finally
            {
                reader.Depth--;
            }

            metadata = builder.ToImmutable();
        }

        return new LazyMetadataWrapper(metadata, LazyMetadataWrapper.Direction.ToOriginalValue, compositionResolver);
    }
}

internal enum ObjectType : byte
{
    String,
    CreationPolicy,
    Type,
    Array,
    Typeless,
    TypeRef,
    Char,
    Guid,
    Enum32Substitution,
    TypeSubstitution,
    TypeArraySubstitution,
}