﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Formatter;

[DebuggerDisplay("{" + nameof(ContractName) + ",nq}")]
[MessagePackFormatter(typeof(ExportDefinitionFormatter))]
public class ExportDefinition : IEquatable<ExportDefinition>
{
    public ExportDefinition(string contractName, IReadOnlyDictionary<string, object?> metadata)
    {
        Requires.NotNullOrEmpty(contractName, nameof(contractName));
        Requires.NotNull(metadata, nameof(metadata));

        this.ContractName = contractName;

        // Don't call ToImmutableDictionary() on the metadata. We have to trust that it's immutable
        // because forcing it to be immutable can defeat LazyMetadataWrapper's laziness, forcing
        // assembly loads and copying a dictionary when it's for practical interests immutable underneath anyway.
        this.Metadata = metadata;
    }

    public string ContractName { get; private set; }

    public IReadOnlyDictionary<string, object?> Metadata { get; private set; }

    public override bool Equals(object? obj)
    {
        return this.Equals(obj as ExportDefinition);
    }

    public override int GetHashCode()
    {
        return this.ContractName.GetHashCode();
    }

    public bool Equals(ExportDefinition? other)
    {
        if (other == null)
        {
            return false;
        }

        bool result = this.ContractName == other.ContractName
            && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata);
        return result;
    }

    public void ToString(TextWriter writer)
    {
        var indentingWriter = IndentingTextWriter.Get(writer);
        indentingWriter.WriteLine("ContractName: {0}", this.ContractName);
        indentingWriter.WriteLine("Metadata:");
        using (indentingWriter.Indent())
        {
            foreach (var item in this.Metadata)
            {
                indentingWriter.WriteLine("{0} = {1}", item.Key, item.Value);
            }
        }
    }

    internal void GetInputAssemblies(ISet<AssemblyName> assemblies, Func<Assembly, AssemblyName> nameGetter)
    {
        Requires.NotNull(assemblies, nameof(assemblies));

        ReflectionHelpers.GetInputAssembliesFromMetadata(assemblies, this.Metadata, nameGetter);
    }

    private class ExportDefinitionFormatter : IMessagePackFormatter<ExportDefinition?>
    {
        public static readonly ExportDefinitionFormatter Instance = new();

        private ExportDefinitionFormatter()
        {
        }

        /// <inheritdoc/>
        public ExportDefinition? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);

            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 2)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ExportDefinition)}. Expected: {2}, Actual: {actualCount}");
                }

                string contractName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                IReadOnlyDictionary<string, object?> metadata = MetadataDictionaryFormatter.Instance.Deserialize(ref reader, options);

                return new ExportDefinition(contractName, metadata);
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, ExportDefinition? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(2);

            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ContractName, options);
            MetadataDictionaryFormatter.Instance.Serialize(ref writer, value.Metadata, options);
        }
    }
}
