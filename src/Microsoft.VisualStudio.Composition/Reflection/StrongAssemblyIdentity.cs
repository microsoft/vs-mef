// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using MessagePack;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;
    using MessagePack.Formatters;


    /// <summary>
    /// Metadata about a <see cref="Assembly"/> that is used to determine if
    /// two assemblies are equivalent.
    /// </summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    //[MessagePackObject]
    [MessagePackFormatter(typeof(StrongAssemblyIdentityFormatter))]

    public class StrongAssemblyIdentity : IEquatable<StrongAssemblyIdentity>
    {

        class StrongAssemblyIdentityFormatter : IMessagePackFormatter<StrongAssemblyIdentity>
        {
            public void Serialize(ref MessagePackWriter writer, StrongAssemblyIdentity value, MessagePackSerializerOptions options)
            {
                options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value.Mvid, options);


                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.FullName.ToString(), options);
                //options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Name.ToString(), options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Version.ToString(), options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CultureName.ToString(), options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.ProcessorArchitecture.ToString(), options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.Flags.ToString(), options);
                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name.CodeBase.ToString(), options);



                //    options.Resolver.GetFormatterWithVerify<AssemblyName>().Serialize(ref writer, value.Name, options);


            }

            public StrongAssemblyIdentity Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {

                var Mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);

                var FullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

                AssemblyName assemblyName = new AssemblyName(FullName);
                //assemblyName.Name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                assemblyName.Version = new Version(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
                assemblyName.CultureName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                assemblyName.ProcessorArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
                assemblyName.Flags = (AssemblyNameFlags)Enum.Parse(typeof(AssemblyNameFlags), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));

                assemblyName.CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);





                return new StrongAssemblyIdentity(assemblyName, Mvid);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StrongAssemblyIdentity"/> class.
        /// </summary>
        /// <param name="name">The assembly name. Cannot be null.</param>
        /// <param name="mvid">The MVID of the ManifestModule of the assembly.</param>
        public StrongAssemblyIdentity(AssemblyName name, Guid mvid)
        {
            Requires.NotNull(name, nameof(name));
            this.Name = name;
            this.Mvid = mvid;
        }

        /// <summary>
        /// Gets the assembly's full name.
        /// </summary>
     //   [Key(0)]
        public AssemblyName Name { get; }

        /// <summary>
        /// Gets the MVID for the assembly's manifest module. This is a unique identifier that represents individual
        /// builds of an assembly.
        /// </summary>
       // [Key(1)]
        public Guid Mvid { get; }

        /// <summary>
        /// Gets the metadata from an assembly at the specified path.
        /// </summary>
        /// <param name="assemblyFile">The path to the assembly to read metadata from.</param>
        /// <param name="assemblyName">The assembly name, if already known; otherwise <see langword="null"/>.</param>
        /// <returns>The assembly metadata.</returns>
        /// <exception cref="FileNotFoundException">Thrown if <paramref name="assemblyFile"/> does not refer to an existing file.</exception>
        public static StrongAssemblyIdentity CreateFrom(string assemblyFile, AssemblyName? assemblyName)
        {
            Requires.NotNullOrEmpty(assemblyFile, nameof(assemblyFile));

            if (assemblyName == null)
            {
                assemblyName = AssemblyName.GetAssemblyName(assemblyFile);
            }

            Guid mvid = GetMvid(assemblyFile);

            return new StrongAssemblyIdentity(assemblyName, mvid);
        }

        /// <summary>
        /// Gets the metadata from an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to read metadata from.</param>
        /// <param name="assemblyName">An optional <see cref="AssemblyName"/> that may be important for dynamic assemblies to find their CodeBase.</param>
        /// <returns>The assembly metadata.</returns>
        public static StrongAssemblyIdentity CreateFrom(Assembly assembly, AssemblyName? assemblyName)
        {
            Requires.NotNull(assembly, nameof(assembly));

            if (assemblyName == null)
            {
                assemblyName = assembly.GetName();
            }

            return new StrongAssemblyIdentity(assemblyName, assembly.ManifestModule.ModuleVersionId);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => this.Equals(obj as StrongAssemblyIdentity);

        /// <inheritdoc/>
        public bool Equals(StrongAssemblyIdentity? other) => this.Equals(other, allowMvidMismatch: false);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.Mvid.GetHashCode();
        }

        internal bool Equals(StrongAssemblyIdentity? other, bool allowMvidMismatch)
        {
            return other != null
                && ByValueEquality.AssemblyNameNoFastCheck.Equals(this.Name, other.Name)
                && (allowMvidMismatch || this.Mvid == other.Mvid);
        }

        /// <summary>
        /// Gets the MVID for an assembly with the specified path.
        /// </summary>
        /// <param name="assemblyFile">The assembly to get the MVID from.</param>
        /// <returns>The MVID.</returns>
        private static Guid GetMvid(string assemblyFile)
        {
            using (var stream = File.OpenRead(assemblyFile))
            {
                using (var reader = new PEReader(stream))
                {
                    var metadataReader = reader.GetMetadataReader();
                    var mvidHandle = metadataReader.GetModuleDefinition().Mvid;
                    return metadataReader.GetGuid(mvidHandle);
                }
            }
        }
    }
}
