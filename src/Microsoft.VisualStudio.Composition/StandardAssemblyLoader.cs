// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Generic;
    using System.Reflection;
#if NET
    using System.Runtime.Loader;
#endif

    /// <summary>
    /// A typical .NET Framework implementation of the <see cref="IAssemblyLoader"/> interface.
    /// </summary>
    internal class StandardAssemblyLoader : IAssemblyLoader
    {
        /// <summary>
        /// A cache of assembly names to loaded assemblies.
        /// </summary>
        private readonly Dictionary<AssemblyName, Assembly> loadedAssemblies = new Dictionary<AssemblyName, Assembly>(ByValueEquality.AssemblyName);

        /// <inheritdoc />
        public Assembly LoadAssembly(AssemblyName assemblyName)
        {
            Assembly? assembly;
            lock (this.loadedAssemblies)
            {
                this.loadedAssemblies.TryGetValue(assemblyName, out assembly);
            }

            if (assembly == null)
            {
                assembly = Assembly.Load(assemblyName);

                lock (this.loadedAssemblies)
                {
                    this.loadedAssemblies[assemblyName] = assembly;
                }
            }

            return assembly;
        }

        /// <inheritdoc />
        public Assembly LoadAssembly(string assemblyFullName, string? codeBasePath)
        {
            Requires.NotNullOrEmpty(assemblyFullName, nameof(assemblyFullName));

            var assemblyName = new AssemblyName(assemblyFullName);
            if (!string.IsNullOrEmpty(codeBasePath))
            {
                assemblyName.CodeBase = codeBasePath;

#if NET
                // On Core CLR, Assembly.Load(AssemblyName) doesn't respect the CodeBase property,
                // so we have to use AssemblyLoadContext.LoadFromAssemblyPath instead.
                // But we'll only resort to that if the ALC doesn't already have a preferred location from which to load the assembly.
                try
                {
                    return this.LoadAssembly(assemblyName);
                }
                catch
                {
                    // Now that the ALC failed to find the assembly, try to load it from the code base path.
                    AssemblyLoadContext alc = AssemblyLoadContext.CurrentContextualReflectionContext ?? AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())!;
                    Assembly assembly = alc.LoadFromAssemblyPath(codeBasePath);
                    lock (this.loadedAssemblies)
                    {
                        this.loadedAssemblies[assemblyName] = assembly;
                    }
                }
#endif
            }

            return this.LoadAssembly(assemblyName);
        }
    }
}
