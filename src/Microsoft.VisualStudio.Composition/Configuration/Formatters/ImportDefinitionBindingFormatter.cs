﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;

internal class ImportDefinitionBindingFormatter : IMessagePackFormatter<ImportDefinitionBinding?>
{
    public static readonly ImportDefinitionBindingFormatter Instance = new();

    private ImportDefinitionBindingFormatter()
    {
    }

    /// <inheritdoc/>
    public ImportDefinitionBinding? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        options.Security.DepthStep(ref reader);

        try
        {
            var actualCount = reader.ReadArrayHeader();
            if (actualCount != 6)
            {
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(ImportDefinitionBinding)}. Expected: {6}, Actual: {actualCount}");
            }

            ImportDefinition importDefinition = options.Resolver.GetFormatterWithVerify<ImportDefinition>().Deserialize(ref reader, options);
            IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

            TypeRef part = typeRefFormatter.Deserialize(ref reader, options);
            TypeRef importingSiteTypeRef = typeRefFormatter.Deserialize(ref reader, options);
            TypeRef importingSiteTypeWithoutCollectionRef = typeRefFormatter.Deserialize(ref reader, options);

            MemberRef? member;
            ParameterRef? parameter;

            if (!reader.TryReadNil())
            {
                member = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
                reader.Skip(); // for ParameterRef
                return new ImportDefinitionBinding(importDefinition, part, member ?? throw new MessagePackSerializationException($"Unexpected null for the type {nameof(MemberRef)}"), importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
            }
            else
            {
                parameter = options.Resolver.GetFormatterWithVerify<ParameterRef?>().Deserialize(ref reader, options);
                return new ImportDefinitionBinding(importDefinition, part, parameter ?? throw new MessagePackSerializationException($"Unexpected null for the type {nameof(ParameterRef)}"), importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
            }
        }
        finally
        {
            reader.Depth--;
        }
    }

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, ImportDefinitionBinding? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(6);

        options.Resolver.GetFormatterWithVerify<ImportDefinition>().Serialize(ref writer, value.ImportDefinition, options);
        IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

        typeRefFormatter.Serialize(ref writer, value.ComposablePartTypeRef, options);
        typeRefFormatter.Serialize(ref writer, value.ImportingSiteTypeRef, options);
        typeRefFormatter.Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);

        if (value.ImportingMemberRef is null)
        {
            writer.WriteNil();
            options.Resolver.GetFormatterWithVerify<ParameterRef?>().Serialize(ref writer, value.ImportingParameterRef, options);
        }
        else
        {
            options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.ImportingMemberRef, options);
            writer.WriteNil(); // for ParameterRef
        }
    }
}
