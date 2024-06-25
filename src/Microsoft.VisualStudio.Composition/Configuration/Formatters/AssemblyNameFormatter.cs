// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter;

using System.Reflection;
using MessagePack;
using MessagePack.Formatters;

internal class AssemblyNameFormatter : IMessagePackFormatter<AssemblyName?>
{
    public static readonly AssemblyNameFormatter Instance = new();

    private AssemblyNameFormatter()
    {
    }

    /// <inheritdoc/>
    public AssemblyName? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        options.Security.DepthStep(ref reader);

        try
        {
            int actualCount = reader.ReadArrayHeader();
            if (actualCount != 1)
            {
                throw new MessagePackSerializationException($"Invalid array count for type {nameof(StrongAssemblyIdentity)}. Expected: {1}, Actual: {actualCount}");
            }

            string fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

            return new AssemblyName(fullName);
        }
        finally
        {
            reader.Depth--;
        }
    }

    /// <inheritdoc/>
    public void Serialize(ref MessagePackWriter writer, AssemblyName? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.FullName, options);
    }
}
