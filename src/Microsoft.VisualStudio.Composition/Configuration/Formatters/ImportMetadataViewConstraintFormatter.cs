// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

#pragma warning disable CS8604 // Possible null reference argument.

    internal class ImportMetadataViewConstraintFormatter : BaseMessagePackFormatter<ImportMetadataViewConstraint>
    {
        protected override ImportMetadataViewConstraint DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            this.CheckArrayHeaderCount(ref reader, 1);
            ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement> requirements = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>>().Deserialize(ref reader, options);
            return new ImportMetadataViewConstraint(requirements, options.CompositionResolver());
        }

        protected override void SerializeData(ref MessagePackWriter writer, ImportMetadataViewConstraint value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>>().Serialize(ref writer, value.Requirements, options);
        }
    }
}
