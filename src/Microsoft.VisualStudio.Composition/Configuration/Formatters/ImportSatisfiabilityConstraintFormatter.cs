// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

#pragma warning disable CS3001 // Argument type is not CLS-compliant

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
                    int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

                    ImmutableDictionary<string, ImportMetadataViewConstraint.MetadatumRequirement>.Builder requirements = ImmutableDictionary.CreateBuilder<string, ImportMetadataViewConstraint.MetadatumRequirement>();
                    for (int i = 0; i < count; i++)
                    {
                        string name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        TypeRef valueTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
                        bool isRequired = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                        requirements.Add(name, new ImportMetadataViewConstraint.MetadatumRequirement(valueTypeRef, isRequired));
                    }

                    return new ImportMetadataViewConstraint(requirements.ToImmutable(), options.CompositionResolver());

                case ConstraintTypes.ExportTypeIdentityConstraint:
                    string? exportTypeIdentity = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    return new ExportTypeIdentityConstraint(exportTypeIdentity);

                case ConstraintTypes.PartCreationPolicyConstraint:
                    CreationPolicy creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
                    return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(creationPolicy)!;

                case ConstraintTypes.ExportMetadataValueImportConstraint:
                    {
                        string? name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
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

            if (value is ImportMetadataViewConstraint importMetadataViewConstraint)
            {
                type = ConstraintTypes.ImportMetadataViewConstraint;
                options.Resolver.GetFormatterWithVerify<ConstraintTypes>().Serialize(ref writer, type, options);

                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, importMetadataViewConstraint.Requirements.Count, options);

                foreach (KeyValuePair<string, ImportMetadataViewConstraint.MetadatumRequirement> item in importMetadataViewConstraint.Requirements)
                {
                    options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, item.Key, options);
                    options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, item.Value.MetadatumValueTypeRef, options);
                    options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, item.Value.IsMetadataumValueRequired, options);
                }
            }
            else if (value is ExportTypeIdentityConstraint exportTypeIdentityConstraint)
            {
                type = ConstraintTypes.ExportTypeIdentityConstraint;
                options.Resolver.GetFormatterWithVerify<ConstraintTypes>().Serialize(ref writer, type, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, exportTypeIdentityConstraint.TypeIdentityName, options);
            }
            else if (value is PartCreationPolicyConstraint partCreationPolicyConstraint)
            {
                type = ConstraintTypes.PartCreationPolicyConstraint;
                options.Resolver.GetFormatterWithVerify<ConstraintTypes>().Serialize(ref writer, type, options);

                options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, partCreationPolicyConstraint.RequiredCreationPolicy, options);
            }
            else if (value is ExportMetadataValueImportConstraint exportMetadataValueImportConstraint)
            {
                type = ConstraintTypes.ExportMetadataValueImportConstraint;
                options.Resolver.GetFormatterWithVerify<ConstraintTypes>().Serialize(ref writer, type, options);

                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, exportMetadataValueImportConstraint.Name, options);
                options.Resolver.GetFormatterWithVerify<object?>().Serialize(ref writer, exportMetadataValueImportConstraint.Value, options);
            }
            else
            {
                throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.ImportConstraintTypeNotSupported, value.GetType().FullName));
            }
        }

        public enum ConstraintTypes
        {
            ImportMetadataViewConstraint,
            ExportTypeIdentityConstraint,
            PartCreationPolicyConstraint,
            ExportMetadataValueImportConstraint,
        }
    }
}
