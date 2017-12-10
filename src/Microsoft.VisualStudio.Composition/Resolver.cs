// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    public class Resolver
    {
        /// <summary>
        /// A <see cref="Resolver"/> instance that should only be used in code paths
        /// that serve for *debugging* purposes.
        /// </summary>
        public static readonly Resolver DefaultInstance = new Resolver(new StandardAssemblyLoader());

        /// <summary>
        /// A cache of TypeRef instances that correspond to Type instances.
        /// </summary>
        /// <remarks>
        /// This is for efficiency to avoid duplicates where convenient to do so.
        /// It is not intended as a guarantee of reference equality across equivalent TypeRef instances.
        /// </remarks>
        internal readonly Dictionary<Type, WeakReference<Reflection.TypeRef>> InstanceCache = new Dictionary<Type, WeakReference<Reflection.TypeRef>>();

        public Resolver(IAssemblyLoader assemblyLoader)
        {
            Requires.NotNull(assemblyLoader, nameof(assemblyLoader));

            this.AssemblyLoader = assemblyLoader;
        }

        internal IAssemblyLoader AssemblyLoader { get; }
    }
}
