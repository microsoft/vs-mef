// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System.Collections.Generic;
using System.IO;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Formatter;

[MessagePackFormatter(typeof(ExportMetadataValueImportConstraintFormatter))]
public class ExportMetadataValueImportConstraint : IImportSatisfiabilityConstraint, IDescriptiveToString
{
    public ExportMetadataValueImportConstraint(string name, object? value)
    {
        Requires.NotNullOrEmpty(name, nameof(name));

        this.Name = name;
        this.Value = value;
    }

    public string Name { get; private set; }

    public object? Value { get; private set; }

    public bool IsSatisfiedBy(ExportDefinition exportDefinition)
    {
        Requires.NotNull(exportDefinition, nameof(exportDefinition));

        object? exportMetadataValue;
        if (exportDefinition.Metadata.TryGetValue(this.Name, out exportMetadataValue))
        {
            if (EqualityComparer<object?>.Default.Equals(this.Value, exportMetadataValue))
            {
                return true;
            }
        }

        return false;
    }

    public bool Equals(IImportSatisfiabilityConstraint? obj)
    {
        var other = obj as ExportMetadataValueImportConstraint;
        if (other == null)
        {
            return false;
        }

        return this.Name == other.Name
            && EqualityComparer<object?>.Default.Equals(this.Value, other.Value);
    }

    public void ToString(TextWriter writer)
    {
        var indentingWriter = IndentingTextWriter.Get(writer);
        indentingWriter.WriteLine("{0} = {1}", this.Name, this.Value);
    }

    private class ExportMetadataValueImportConstraintFormatter : IMessagePackFormatter<ExportMetadataValueImportConstraint?>
    {
        public static readonly ExportMetadataValueImportConstraintFormatter Instance = new();

        private ExportMetadataValueImportConstraintFormatter()
        {
        }

        public ExportMetadataValueImportConstraint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
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
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ExportMetadataValueImportConstraint)}. Expected: {2}, Actual: {actualCount}");
                }

                string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                object? value = options.Resolver.GetFormatterWithVerify<object?>().Deserialize(ref reader, options);
                return new ExportMetadataValueImportConstraint(name, value);
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, ExportMetadataValueImportConstraint? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(2);

            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
            options.Resolver.GetFormatterWithVerify<object?>().Serialize(ref writer, value.Value, options);
        }
    }
}
