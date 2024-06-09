// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;

    internal class StrongAssemblyIdentityFormatter : IMessagePackFormatter<StrongAssemblyIdentity?>
    {
        public static readonly StrongAssemblyIdentityFormatter Instance = new();

        private StrongAssemblyIdentityFormatter()
        {
        }

        /// <inheritdoc/>
        public StrongAssemblyIdentity? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }
            options.Security.DepthStep(ref reader);

            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 3)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(StrongAssemblyIdentity)}. Expected: {3}, Actual: {actualCount}");
                }

                Guid mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
                string fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

                var assemblyName = new AssemblyName(fullName);
                assemblyName.CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                return new StrongAssemblyIdentity(assemblyName, mvid);
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, StrongAssemblyIdentity? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;

            }
            writer.WriteArrayHeader(3);

            options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value!.Mvid, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.FullName, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CodeBase, options);
        }
    }
}
