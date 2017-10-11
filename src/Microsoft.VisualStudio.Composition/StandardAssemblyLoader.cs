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
    internal class StandardAssemblyLoader : IAssemblyLoader
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
#if NETCOREAPP1_0
                // Clone the AssemblyName to undefine the CodeBase property in case it is set.
                // Workaround for https://github.com/dotnet/coreclr/issues/10561
                assemblyName = new AssemblyName
                {
                    Name = assemblyName.Name,
                    ContentType = assemblyName.ContentType,
                    CultureName = assemblyName.CultureName,
                    Flags = assemblyName.Flags,
                    ProcessorArchitecture = assemblyName.ProcessorArchitecture,
                    Version = assemblyName.Version,
                };
#endif
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
#if NET45
            if (!string.IsNullOrEmpty(codeBasePath))
            {
                assemblyName.CodeBase = codeBasePath;
            }
#endif

            return this.LoadAssembly(assemblyName);
        }
    }
}
