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
            // Ankit
            // check                     AssemblyName? name = this.ReadAssemblyName();
            // we need to write Assembly .Name and add srtore in the collection for reuse                 if (this.TryPrepareSerializeReusableObject(assemblyName))
            // check         protected void Write(string? value) we need to add it to colelction as well



            if (MessagePackFormatterContext.TryPrepareDeserializeReusableObject(out uint id, out StrongAssemblyIdentity? value, ref reader, options))
            {
                Guid mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
                string fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

                var assemblyName = new AssemblyName(fullName);
                assemblyName.CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                value = new StrongAssemblyIdentity(assemblyName, mvid);

                MessagePackFormatterContext.OnDeserializedReusableObject(id, value);
            }

            return value;

            // AssemblyName? name =  assemblyName;
            //var assemblyName = new AssemblyName(fullName)
            //{
            //    Version = new Version(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options)),
            //    CultureName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options),
            //    ProcessorArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options)),
            //    Flags = (AssemblyNameFlags)Enum.Parse(typeof(AssemblyNameFlags), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options)),
            //    CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options),
            //};
        }

        public void Serialize(ref MessagePackWriter writer, StrongAssemblyIdentity value, MessagePackSerializerOptions options)
        {
            if (MessagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, options))
            {
                options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value.Mvid, options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.FullName, options);
                //options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Version.ToString(), options);
                //options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CultureName.ToString(), options);
                //options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.ProcessorArchitecture.ToString(), options);
                //options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Flags.ToString(), options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CodeBase.ToString(), options);
            }
        }
    }
}
