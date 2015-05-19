namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    public class MyResolver
    {
        /// <summary>
        /// A <see cref="MyResolver"/> instance that should only be used in code paths
        /// that serve for *debugging* purposes.
        /// </summary>
        public static readonly MyResolver DefaultInstance = new MyResolver(new StandardAssemblyLoader());

        /// <summary>
        /// A cache of TypeRef instances that correspond to Type instances.
        /// </summary>
        /// <remarks>
        /// This is for efficiency to avoid duplicates where convenient to do so.
        /// It is not intended as a guarantee of reference equality across equivalent TypeRef instances.
        /// </remarks>
        internal readonly Dictionary<Type, WeakReference<TypeRef>> InstanceCache = new Dictionary<Type, WeakReference<TypeRef>>();

        private readonly IAssemblyLoader assemblyLoader;

        private MyResolver(IAssemblyLoader assemblyLoader)
        {
            Requires.NotNull(assemblyLoader, nameof(assemblyLoader));
            this.assemblyLoader = assemblyLoader;
        }

        public static MyResolver Get(IAssemblyLoader assemblyLoader)
        {
            Requires.NotNull(assemblyLoader, nameof(assemblyLoader));

            return (assemblyLoader as MyResolver) ?? new MyResolver(assemblyLoader);
        }

        internal Module GetManifest(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, nameof(assemblyName));

            var assembly = this.assemblyLoader.LoadAssembly(assemblyName);
            return assembly.ManifestModule;
        }
    }
}
