// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ExportMetadataValueImportConstraintFormatter : BaseMessagePackFormatter<ExportMetadataValueImportConstraint>
    {
        public static readonly ExportMetadataValueImportConstraintFormatter Instance = new();

        private ExportMetadataValueImportConstraintFormatter()
            : base(arrayElementCount: 2)
        {
        }

        protected override ExportMetadataValueImportConstraint DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            object? value = options.Resolver.GetFormatterWithVerify<object?>().Deserialize(ref reader, options);
            string name = reader.ReadString()!;
            return new ExportMetadataValueImportConstraint(name, value);
        }

        protected override void SerializeData(ref MessagePackWriter writer, ExportMetadataValueImportConstraint value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<object?>().Serialize(ref writer, value.Value, options);
            writer.Write(value.Name);
        }
    }
}
