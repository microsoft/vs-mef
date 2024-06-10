// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using MessagePack;
    using MessagePack.Formatters;

    internal class ExportTypeIdentityConstraintFormatter : IMessagePackFormatter<ExportTypeIdentityConstraint?>
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
