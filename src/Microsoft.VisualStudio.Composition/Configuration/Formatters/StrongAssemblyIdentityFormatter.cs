// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;

    internal class StrongAssemblyIdentityFormatter : BaseMessagePackFormatter<StrongAssemblyIdentity?>
    {
        public static readonly StrongAssemblyIdentityFormatter Instance = new();

        private StrongAssemblyIdentityFormatter()
        {
        }

        /// <inheritdoc/>
        protected override StrongAssemblyIdentity? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out StrongAssemblyIdentity? value, ref reader))
            {
                this.CheckArrayHeaderCount(ref reader, 3);
                Guid mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
                string fullName = reader.ReadString()!;

                var assemblyName = new AssemblyName(fullName);
                assemblyName.CodeBase = reader.ReadString()!;
                value = new StrongAssemblyIdentity(assemblyName, mvid);
                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, StrongAssemblyIdentity? value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                writer.WriteArrayHeader(3);
                options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value!.Mvid, options);
                writer.Write(value.Name.FullName);
                writer.Write(value.Name.CodeBase!.ToString());
            }
        }
    }
}
