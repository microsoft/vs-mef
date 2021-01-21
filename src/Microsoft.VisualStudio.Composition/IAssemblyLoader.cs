// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Supplies the functionality for loading assemblies.
    /// </summary>
    /// <remarks>
    /// Implementations MUST be thread-safe and should be very fast for assemblies
    /// that have already been loaded.
    /// </remarks>
    public interface IAssemblyLoader
    {
        /// <summary>
        /// Loads an assembly with the specified name and path.
        /// </summary>
        /// <param name="assemblyFullName">The full name of the assembly, as might be found in the <see cref="AssemblyName.FullName"/> property.</param>
        /// <param name="codeBasePath">The path to the assembly to load. May be null.</param>
        /// <returns>The loaded assembly. Never <c>null</c>.</returns>
        /// <exception cref="Exception">May be thrown if the assembly cannot be found or fails to load.</exception>
        Assembly LoadAssembly(string assemblyFullName, string codeBasePath);

        /// <summary>
        /// Loads an assembly with the specified name.
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <returns>The loaded assembly. Never <c>null</c>.</returns>
        /// <exception cref="Exception">May be thrown if the assembly cannot be found or fails to load.</exception>
        Assembly LoadAssembly(AssemblyName assemblyName);
    }
}
