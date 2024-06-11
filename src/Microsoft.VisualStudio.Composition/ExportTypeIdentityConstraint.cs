﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.VisualStudio.Composition.Formatter;
using Microsoft.VisualStudio.Composition.Reflection;

[MessagePackFormatter(typeof(ExportTypeIdentityConstraintFormatter))]
public class ExportTypeIdentityConstraint : IImportSatisfiabilityConstraint, IDescriptiveToString
{
    public ExportTypeIdentityConstraint(Type typeIdentity)
    {
        Requires.NotNull(typeIdentity, nameof(typeIdentity));
        this.TypeIdentityName = ContractNameServices.GetTypeIdentity(typeIdentity);
    }

    public ExportTypeIdentityConstraint(string typeIdentityName)
    {
        Requires.NotNullOrEmpty(typeIdentityName, nameof(typeIdentityName));
        this.TypeIdentityName = typeIdentityName;
    }

    public string TypeIdentityName { get; private set; }

    public static ImmutableDictionary<string, object?> GetExportMetadata(Type type)
    {
        Requires.NotNull(type, nameof(type));

        return GetExportMetadata(ContractNameServices.GetTypeIdentity(type));
    }

    public static ImmutableDictionary<string, object?> GetExportMetadata(string typeIdentity)
    {
        Requires.NotNullOrEmpty(typeIdentity, nameof(typeIdentity));

        return ImmutableDictionary<string, object?>.Empty.Add(CompositionConstants.ExportTypeIdentityMetadataName, typeIdentity);
    }

    public bool IsSatisfiedBy(ExportDefinition exportDefinition)
    {
        Requires.NotNull(exportDefinition, nameof(exportDefinition));

        string? value;
        if (exportDefinition.Metadata.TryGetValue(CompositionConstants.ExportTypeIdentityMetadataName, out value))
        {
            return this.TypeIdentityName == value;
        }

        return false;
    }

    public void ToString(TextWriter writer)
    {
        var indentingWriter = IndentingTextWriter.Get(writer);
        indentingWriter.WriteLine("TypeIdentityName: {0}", this.TypeIdentityName);
    }

    public bool Equals(IImportSatisfiabilityConstraint? obj)
    {
        var other = obj as ExportTypeIdentityConstraint;
        if (other == null)
        {
            return false;
        }

        return this.TypeIdentityName == other.TypeIdentityName;
    }

    private class ExportTypeIdentityConstraintFormatter : IMessagePackFormatter<ExportTypeIdentityConstraint?>
    {
        public static readonly ExportTypeIdentityConstraintFormatter Instance = new();

        private ExportTypeIdentityConstraintFormatter()
        {
        }

        public ExportTypeIdentityConstraint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);

            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 1)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(ExportTypeIdentityConstraint)}. Expected: {1}, Actual: {actualCount}");
                }

                string typeIdentityName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                return new ExportTypeIdentityConstraint(typeIdentityName);
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, ExportTypeIdentityConstraint? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);

            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.TypeIdentityName, options);
        }
    }
}
