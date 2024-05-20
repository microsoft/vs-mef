// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using MessagePack;

    public class Resolver
    {
        /// <summary>
        /// A <see cref="Resolver"/> instance that may be used where customizing assembly loading is not necessary.
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

        internal readonly Dictionary<Assembly, AssemblyName> NormalizedAssemblyCache = new Dictionary<Assembly, AssemblyName>();

        /// <summary>
        /// A map of assemblies loaded by VS MEF and their metadata.
        /// </summary>
        private readonly Dictionary<AssemblyName, StrongAssemblyIdentity> loadedAssemblyStrongIdentities = new Dictionary<AssemblyName, StrongAssemblyIdentity>(ByValueEquality.AssemblyName);

        public Resolver(IAssemblyLoader assemblyLoader)
        {
            Requires.NotNull(assemblyLoader, nameof(assemblyLoader));

            this.AssemblyLoader = assemblyLoader;
        }

        internal IAssemblyLoader AssemblyLoader { get; }

        /// <summary>
        /// Gets identity and version metadata for an assembly with the given <see cref="AssemblyName"/>,
        /// if that assembly has been loaded with this <see cref="Resolver"/>.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to look up.</param>
        /// <param name="assemblyId">Receives the metadata from the assembly, if it has been loaded by this <see cref="Resolver"/>; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the metadata was found; <see langword="false"/> otherwise.</returns>
        internal bool TryGetAssemblyId(AssemblyName assemblyName, [NotNullWhen(true)] out StrongAssemblyIdentity? assemblyId)
        {
            lock (this.loadedAssemblyStrongIdentities)
            {
                return this.loadedAssemblyStrongIdentities.TryGetValue(assemblyName, out assemblyId);
            }
        }

        /// <summary>
        /// Determines the strong identity of an assembly and stores it.
        /// </summary>
        /// <param name="assembly">The loaded assembly.</param>
        /// <param name="assemblyName">An optional <see cref="AssemblyName"/> that may be important for dynamic assemblies to find their CodeBase.</param>
        /// <returns>The identity determined for this assembly.</returns>
        internal StrongAssemblyIdentity GetStrongAssemblyIdentity(Assembly assembly, AssemblyName? assemblyName) => this.NotifyAssemblyLoaded(assembly, assemblyName);

        /// <summary>
        /// Determines the strong identity of an assembly and stores it.
        /// </summary>
        /// <param name="assembly">The loaded assembly.</param>
        /// <param name="assemblyName">An optional <see cref="AssemblyName"/> that may be important for dynamic assemblies to find their CodeBase.</param>
        /// <returns>The identity determined for this assembly.</returns>
        private StrongAssemblyIdentity NotifyAssemblyLoaded(Assembly assembly, AssemblyName? assemblyName)
        {
            Requires.NotNull(assembly, nameof(assembly));

            if (assemblyName == null)
            {
                assemblyName = this.GetNormalizedAssemblyName(assembly);
            }

            if (this.TryGetAssemblyId(assemblyName, out StrongAssemblyIdentity? result))
            {
                return result;
            }

            var assemblyId = StrongAssemblyIdentity.CreateFrom(assembly, assemblyName);
            lock (this.loadedAssemblyStrongIdentities)
            {
                if (!this.loadedAssemblyStrongIdentities.TryGetValue(assemblyName, out result))
                {
                    this.loadedAssemblyStrongIdentities.Add(assemblyName, assemblyId);
                    result = assemblyId;
                }

                return result;
            }
        }

        /// <summary>
        /// An <see cref="IAssemblyLoader"/> that wraps another, and notifies its creator
        /// whenever an assembly is loaded.
        /// </summary>
       // [MessagePackObject(true)]
        internal class AssemblyLoaderWrapper : IAssemblyLoader
        {
            /// <summary>
            /// The <see cref="Resolver"/> that created this instance.
            /// </summary>
            private readonly Resolver resolver;

            /// <summary>
            /// The inner <see cref="IAssemblyLoader"/> to use.
            /// </summary>
            private readonly IAssemblyLoader inner;

            /// <summary>
            /// Initializes a new instance of the <see cref="AssemblyLoaderWrapper"/> class.
            /// </summary>
            /// <param name="resolver">The <see cref="Resolver"/> that created this instance.</param>
            /// <param name="inner">The inner <see cref="IAssemblyLoader"/> to use.</param>
            internal AssemblyLoaderWrapper(Resolver resolver, IAssemblyLoader inner)
            {
                this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            /// <inheritdoc />
            public Assembly LoadAssembly(string assemblyFullName, string? codeBasePath)
            {
                var assembly = this.inner.LoadAssembly(assemblyFullName, codeBasePath);
                this.resolver.NotifyAssemblyLoaded(assembly, null);
                return assembly;
            }

            /// <inheritdoc />
            public Assembly LoadAssembly(AssemblyName assemblyName)
            {
                var assembly = this.inner.LoadAssembly(assemblyName);
                this.resolver.NotifyAssemblyLoaded(assembly, assemblyName);
                return assembly;
            }
        }

        internal AssemblyName GetNormalizedAssemblyName(Assembly assembly)
        {
            Requires.NotNull(assembly, nameof(assembly));

            lock (this.NormalizedAssemblyCache)
            {
                if (!this.NormalizedAssemblyCache.TryGetValue(assembly, out AssemblyName? assemblyName))
                {
                    assemblyName = GetNormalizedAssemblyName(assembly.GetName());
                    this.NormalizedAssemblyCache[assembly] = assemblyName;
                }

                return assemblyName;
            }
        }

        internal static AssemblyName GetNormalizedAssemblyName(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, nameof(assemblyName));

            AssemblyName? normalizedAssemblyName = assemblyName;
            if (assemblyName.CodeBase?.IndexOf('~') >= 0)
            {
                // Using ToString() rather than AbsoluteUri here to match the CLR's AssemblyName.CodeBase convention of paths without %20 space characters.
                string normalizedCodeBase = new Uri(Path.GetFullPath(new Uri(assemblyName.CodeBase).LocalPath)).ToString();
                normalizedAssemblyName = (AssemblyName)assemblyName.Clone();
                normalizedAssemblyName.CodeBase = normalizedCodeBase;
            }

            return normalizedAssemblyName;
        }
    }
}
