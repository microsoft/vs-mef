namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;

    /// <summary>
    /// Metadata about a <see cref="Assembly"/> that is used to determine if
    /// two assemblies are equivalent.
    /// </summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class StrongAssemblyIdentity : IEquatable<StrongAssemblyIdentity>
    {
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
        public AssemblyName Name { get; }

        /// <summary>
        /// Gets the MVID for the assembly's manifest module. This is a unique identifier that represents individual
        /// builds of an assembly.
        /// </summary>
        public Guid Mvid { get; }

        /// <summary>
        /// Gets the metadata from an assembly at the specified path.
        /// </summary>
        /// <param name="assemblyFile">The path to the assembly to read metadata from.</param>
        /// <param name="assemblyName">The assembly name, if already known; otherwise <c>null</c>.</param>
        /// <returns>The assembly metadata.</returns>
        /// <exception cref="FileNotFoundException">Thrown if <paramref name="assemblyFile"/> does not refer to an existing file.</exception>
        public static StrongAssemblyIdentity CreateFrom(string assemblyFile, AssemblyName assemblyName)
        {
            Requires.NotNullOrEmpty(assemblyFile, nameof(assemblyFile));

            if (assemblyName == null)
            {
#if DESKTOP
                assemblyName = AssemblyName.GetAssemblyName(assemblyFile);
#else
                throw new NotSupportedException($"{nameof(assemblyName)} must be specified on this platform.");
#endif
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
        public static StrongAssemblyIdentity CreateFrom(Assembly assembly, AssemblyName assemblyName)
        {
            Requires.NotNull(assembly, nameof(assembly));

            if (assemblyName == null)
            {
                assemblyName = assembly.GetName();
            }

            return new StrongAssemblyIdentity(assemblyName, assembly.ManifestModule.ModuleVersionId);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => this.Equals(obj as StrongAssemblyIdentity);

        /// <inheritdoc/>
        public bool Equals(StrongAssemblyIdentity other)
        {
            return other != null
                && ByValueEquality.AssemblyNameNoFastCheck.Equals(this.Name, other.Name)
                && this.Mvid == other.Mvid;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.Mvid.GetHashCode();
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
