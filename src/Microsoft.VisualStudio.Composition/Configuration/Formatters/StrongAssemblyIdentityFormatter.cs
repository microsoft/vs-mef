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
            Guid mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
            string fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

            var assemblyName = new AssemblyName(fullName)
            {
                Version = new Version(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options)),
                CultureName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options),
                ProcessorArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options)),
                Flags = (AssemblyNameFlags)Enum.Parse(typeof(AssemblyNameFlags), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options)),
                CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options),
            };
            return new StrongAssemblyIdentity(assemblyName, mvid);
        }

        public void Serialize(ref MessagePackWriter writer, StrongAssemblyIdentity value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value.Mvid, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.FullName.ToString(), options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Version.ToString(), options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CultureName.ToString(), options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.ProcessorArchitecture.ToString(), options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Flags.ToString(), options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CodeBase.ToString(), options);
        }
    }
}
