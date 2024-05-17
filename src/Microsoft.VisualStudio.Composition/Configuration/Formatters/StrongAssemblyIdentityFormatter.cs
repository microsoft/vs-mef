// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;

    public class StrongAssemblyIdentityFormatter : IMessagePackFormatter<StrongAssemblyIdentity>
    {
        /// <inheritdoc/>
        public StrongAssemblyIdentity Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out StrongAssemblyIdentity? value, ref reader, options))
            {
                Guid mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
                string fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

                var assemblyName = new AssemblyName(fullName);
                assemblyName.CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                value = new StrongAssemblyIdentity(assemblyName, mvid);

                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, StrongAssemblyIdentity value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer, options))
            {
                options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value.Mvid, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.FullName, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CodeBase.ToString(), options);
            }
        }
    }
}
