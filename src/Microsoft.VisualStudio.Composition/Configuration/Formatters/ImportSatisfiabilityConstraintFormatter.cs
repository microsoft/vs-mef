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

    internal class ImportSatisfiabilityConstraintFormatter : IMessagePackFormatter<IImportSatisfiabilityConstraint>
    {
        public static readonly ImportSatisfiabilityConstraintFormatter Instance = new();

        private ImportSatisfiabilityConstraintFormatter()
        {
        }

        /// <inheritdoc/>
        public IImportSatisfiabilityConstraint Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            ConstraintTypes type = options.Resolver.GetFormatterWithVerify<ConstraintTypes>().Deserialize(ref reader, options);

            switch (type)
            {
                case ConstraintTypes.ImportMetadataViewConstraint:
                    {
                        int count = reader.ReadInt32();

                        ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>.Builder requirements = ImmutableDictionary.CreateBuilder<string, ImportMetadataViewConstraint.MetadatumRequirement>();
                        IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString()!;
                            TypeRef valueTypeRef = typeRefFormatter.Deserialize(ref reader, options);
                            bool isRequired = reader.ReadBoolean();
                            requirements.Add(name, new ImportMetadataViewConstraint.MetadatumRequirement(valueTypeRef, isRequired));
                        }

                        return new ImportMetadataViewConstraint(requirements.ToImmutable(), options.CompositionResolver());
                    }

                case ConstraintTypes.ExportTypeIdentityConstraint:
                    {
                        string? exportTypeIdentity = reader.ReadString();
                        return new ExportTypeIdentityConstraint(exportTypeIdentity);
                    }

                case ConstraintTypes.PartCreationPolicyConstraint:
                    {
                        CreationPolicy creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
                        return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(creationPolicy)!;
                    }

                case ConstraintTypes.ExportMetadataValueImportConstraint:
                    {
                        string? name = reader.ReadString();
                        object? value = options.Resolver.GetFormatterWithVerify<object?>().Deserialize(ref reader, options);
                        return new ExportMetadataValueImportConstraint(name, value);
                    }

                default:
                    throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.UnexpectedConstraintType, type));
            }

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, IImportSatisfiabilityConstraint value, MessagePackSerializerOptions options)
        {
            ConstraintTypes type;
            IMessagePackFormatter<ConstraintTypes> constraintTypesFormatter = options.Resolver.GetFormatterWithVerify<ConstraintTypes>();

            if (value is ImportMetadataViewConstraint importMetadataViewConstraint)
            {
                type = ConstraintTypes.ImportMetadataViewConstraint;
                constraintTypesFormatter.Serialize(ref writer, type, options);

                writer.Write(importMetadataViewConstraint.Requirements.Count);
                IMessagePackFormatter<TypeRef> typeRefFormatter = options.Resolver.GetFormatterWithVerify<TypeRef>();

                foreach (KeyValuePair<string, ImportMetadataViewConstraint.MetadatumRequirement> item in importMetadataViewConstraint.Requirements)
                {
                    writer.Write(item.Key);
                    typeRefFormatter.Serialize(ref writer, item.Value.MetadatumValueTypeRef, options);
                    writer.Write(item.Value.IsMetadataumValueRequired);
                }
            }
            else if (value is ExportTypeIdentityConstraint exportTypeIdentityConstraint)
            {
                type = ConstraintTypes.ExportTypeIdentityConstraint;
                constraintTypesFormatter.Serialize(ref writer, type, options);
                writer.Write(exportTypeIdentityConstraint.TypeIdentityName);
            }
            else if (value is PartCreationPolicyConstraint partCreationPolicyConstraint)
            {
                type = ConstraintTypes.PartCreationPolicyConstraint;
                constraintTypesFormatter.Serialize(ref writer, type, options);

                options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, partCreationPolicyConstraint.RequiredCreationPolicy, options);
            }
            else if (value is ExportMetadataValueImportConstraint exportMetadataValueImportConstraint)
            {
                type = ConstraintTypes.ExportMetadataValueImportConstraint;
                constraintTypesFormatter.Serialize(ref writer, type, options);

                writer.Write(exportMetadataValueImportConstraint.Name);
                options.Resolver.GetFormatterWithVerify<object?>().Serialize(ref writer, exportMetadataValueImportConstraint.Value, options);
            }
            else
            {
                throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.ImportConstraintTypeNotSupported, value.GetType().FullName));
            }
        }

        internal enum ConstraintTypes
        {
            ImportMetadataViewConstraint,
            ExportTypeIdentityConstraint,
            PartCreationPolicyConstraint,
            ExportMetadataValueImportConstraint,
        }
    }
}
