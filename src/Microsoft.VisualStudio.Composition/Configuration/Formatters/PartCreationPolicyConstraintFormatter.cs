// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class PartCreationPolicyConstraintFormatter : IMessagePackFormatter<PartCreationPolicyConstraint?>
    {
        public static readonly PartCreationPolicyConstraintFormatter Instance = new();

        private PartCreationPolicyConstraintFormatter()
        {
        }

        public PartCreationPolicyConstraint? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
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
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(PartCreationPolicyConstraint)}. Expected: {1}, Actual: {actualCount}");
                }

                CreationPolicy creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
                return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(creationPolicy)!;
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, PartCreationPolicyConstraint? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(1);

            options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, value.RequiredCreationPolicy, options);
        }
    }
}
