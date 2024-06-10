// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter;

using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Reflection;

internal class ParameterRefFormatter : IMessagePackFormatter<ParameterRef?>
{
    public static readonly ParameterRefFormatter Instance = new();

    private ParameterRefFormatter()
    {
    }

    /// <inheritdoc/>
    public ParameterRef? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        try
        {
            var actualCount = reader.ReadArrayHeader();
            if (actualCount != 2)
            {
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(ParameterRef)}. Expected: {2}, Actual: {actualCount}");
            }

            options.Security.DepthStep(ref reader);

            MethodRef method = options.Resolver.GetFormatterWithVerify<MethodRef>().Deserialize(ref reader, options);
            int parameterIndex = reader.ReadInt32();
            return new ParameterRef(method, parameterIndex);
        }
        finally
        {
            reader.Depth--;
        }
    }

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, ParameterRef? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(2);

        options.Resolver.GetFormatterWithVerify<MethodRef>().Serialize(ref writer, value.Method, options);
        writer.Write(value.ParameterIndex);
    }
}
