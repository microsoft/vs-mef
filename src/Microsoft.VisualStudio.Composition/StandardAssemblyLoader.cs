/********************************************************
*                                                        *
*   © Copyright (C) Microsoft. All rights reserved.      *
*                                                        *
*********************************************************/

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// A typical .NET Framework implementation of the <see cref="IAssemblyLoader"/> interface.
    /// </summary>
    public class StandardAssemblyLoader : IAssemblyLoader
    {
        /// <summary>
        /// A cache of assembly names to loaded assemblies.
        /// </summary>
        private readonly Dictionary<AssemblyName, Assembly> loadedAssemblies = new Dictionary<AssemblyName, Assembly>(ByValueEquality.AssemblyName);

        /// <inheritdoc />
        public Assembly LoadAssembly(AssemblyName assemblyName)
        {
            Assembly assembly;
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
        public Assembly LoadAssembly(string assemblyFullName, string codeBasePath)
        {
            Requires.NotNullOrEmpty(assemblyFullName, nameof(assemblyFullName));

            var assemblyName = new AssemblyName(assemblyFullName);
            if (!string.IsNullOrEmpty(codeBasePath))
            {
                assemblyName.CodeBase = codeBasePath;
            }

            return this.LoadAssembly(assemblyName);
        }
    }
}
