// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*
 * Elements of this file taken from:
 * https://github.com/dotnet/buildtools/blob/647d79ca86350646be4b87b889221d9a1de9b710/src/common/AssemblyResolver.cs#L31-L107
 * Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file https://github.com/dotnet/buildtools/blob/master/LICENSE for more information.
 */

#pragma warning disable CA1819 // Properties should not return arrays
#pragma warning disable CA1001 // Types that own disposable fields should be disposable

namespace Microsoft.VisualStudio.Composition.AppHost
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class CreateComposition : AppDomainIsolatedTask, ICancelableTask
    {
        private const string DiscoveryErrorCode = "MEF0001";

        private const string CompositionErrorCode = "MEF0002";

        private readonly Lazy<HashSet<string>> skipCodes;

        /// <summary>
        /// The source of the <see cref="CancellationToken" /> that is canceled when
        /// <see cref="ICancelableTask.Cancel" /> is invoked.
        /// </summary>
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private readonly List<string> writtenFiles = new List<string>();

        /// <summary>
        /// A copy of <see cref="CatalogAssemblies"/> but transformed to be full paths
        /// instead of a mixture of assembly names and various paths.
        /// </summary>
        private readonly List<string> catalogAssemblyPaths = new List<string>();

        /// <summary>
        /// The assemblies that have been found and logged as to their locations already.
        /// </summary>
        private readonly HashSet<Assembly> loggedAssemblies = new HashSet<Assembly>();

        /// <summary>
        /// The names of assemblies that could not be found and have been logged.
        /// </summary>
        private readonly HashSet<string> loggedMissingAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public CreateComposition()
        {
            this.skipCodes = new Lazy<HashSet<string>>(() => new HashSet<string>(this.NoWarn, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>Gets a token that is canceled when MSBuild is requesting that we abort.</summary>
        public CancellationToken CancellationToken => this.cts.Token;

        public ITaskItem[] CatalogAssemblies { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Gets or sets the paths to assemblies that may be loaded as part of MEF discovery (because they are referenced by an assembly in the <see cref="CatalogAssemblies"/>.)
        /// </summary>
        public ITaskItem[] ReferenceAssemblies { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Gets or sets a list of paths to directories to search for MEF catalog assemblies.
        /// </summary>
        public ITaskItem[] CatalogAssemblySearchPath { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Gets or sets a list of codes to suppress warnings for.
        /// </summary>
        public string[]? NoWarn { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to continue when errors occur while scanning MEF assemblies.
        /// </summary>
        /// <value>The default is <c>false</c>, causing build failure.</value>
        public bool ContinueOnDiscoveryErrors { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to continue when errors occur while composing the graph.
        /// </summary>
        /// <value>The default is <c>false</c>, causing build failure.</value>
        public bool ContinueOnCompositionErrors { get; set; }

        [Required]
        public string CompositionCacheFile { get; set; } = null!;

        public string? DgmlOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the directory to write the discovery and composition log files.
        /// </summary>
        [Required]
        public string LogOutputPath { get; set; } = null!;

        /// <summary>
        /// Gets or sets a list of files that were written during this task's execution.
        /// </summary>
        [Output]
        public ITaskItem[]? FileWrites { get; set; }

        /// <inheritdoc />
        public void Cancel() => this.cts.Cancel();

        public override bool Execute()
        {
            if (Environment.GetEnvironmentVariable("CreateCompositionTaskDebug") == "1")
            {
                Debugger.Launch();
            }

            this.catalogAssemblyPaths.AddRange(this.CatalogAssemblies.Select(this.GetMEFAssemblyFullPath));

            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
            try
            {
                var loadableAssemblies = this.catalogAssemblyPaths
                    .Concat(this.ReferenceAssemblies.Select(i => i.GetMetadata("FullPath")) ?? Enumerable.Empty<string>());
                var resolver = new Resolver(new AssemblyLoader(this, loadableAssemblies));
                var discovery = PartDiscovery.Combine(
                    new AttributedPartDiscoveryV1(resolver),
                    new AttributedPartDiscovery(resolver, isNonPublicSupported: true));

                this.CancellationToken.ThrowIfCancellationRequested();

                var parts = discovery.CreatePartsAsync(this.catalogAssemblyPaths).GetAwaiter().GetResult();
                var catalog = ComposableCatalog.Create(resolver)
                    .AddParts(parts);

                this.LogLines(this.GetLogFilePath("CatalogAssemblies"), this.GetCatalogAssembliesLines(catalog), this.CancellationToken);

                string catalogErrorFilePath = this.GetLogFilePath("CatalogErrors");
                if (catalog.DiscoveredParts.DiscoveryErrors.IsEmpty)
                {
                    File.Delete(catalogErrorFilePath);
                }
                else
                {
                    this.LogLines(catalogErrorFilePath, GetCatalogErrorLines(catalog), this.CancellationToken);
                    foreach (var error in catalog.DiscoveredParts.DiscoveryErrors)
                    {
                        string message = error.GetUserMessage();
                        if (this.ContinueOnDiscoveryErrors)
                        {
                            this.LogWarning(DiscoveryErrorCode, message);
                        }
                        else
                        {
                            this.Log.LogError(null, DiscoveryErrorCode, null, null, 0, 0, 0, 0, message);
                        }
                    }

                    if (!this.ContinueOnDiscoveryErrors)
                    {
                        return false;
                    }
                }

                this.CancellationToken.ThrowIfCancellationRequested();
                var configuration = CompositionConfiguration.Create(catalog);

                if (!string.IsNullOrEmpty(this.DgmlOutputPath))
                {
                    configuration.CreateDgml().Save(this.DgmlOutputPath);
                    this.writtenFiles.Add(this.DgmlOutputPath!);
                }

                this.CancellationToken.ThrowIfCancellationRequested();
                string compositionLogPath = this.GetLogFilePath("CompositionErrors");
                if (configuration.CompositionErrors.IsEmpty)
                {
                    File.Delete(compositionLogPath);
                }
                else
                {
                    this.LogLines(compositionLogPath, GetCompositionErrorLines(configuration), this.CancellationToken);
                    foreach (var error in configuration.CompositionErrors.Peek())
                    {
                        if (this.ContinueOnCompositionErrors)
                        {
                            this.LogWarning(CompositionErrorCode, error.Message);
                        }
                        else
                        {
                            this.Log.LogError(null, CompositionErrorCode, null, null, 0, 0, 0, 0, error.Message);
                        }
                    }

                    if (!this.ContinueOnCompositionErrors)
                    {
                        return false;
                    }
                }

                this.CancellationToken.ThrowIfCancellationRequested();

                string cachePath = Path.GetFullPath(this.CompositionCacheFile);
                this.Log.LogMessage("Producing IoC container \"{0}\"", cachePath);
                using (var cacheStream = File.Open(cachePath, FileMode.Create))
                {
                    this.CancellationToken.ThrowIfCancellationRequested();
                    var runtime = RuntimeComposition.CreateRuntimeComposition(configuration);
                    this.CancellationToken.ThrowIfCancellationRequested();
                    var runtimeCache = new CachedComposition();
                    runtimeCache.SaveAsync(runtime, cacheStream, this.CancellationToken).GetAwaiter().GetResult();
                }

                this.writtenFiles.Add(cachePath);

                return !this.Log.HasLoggedErrors;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= this.CurrentDomain_AssemblyResolve;
                this.FileWrites = this.writtenFiles.Select(f => new TaskItem(f)).ToArray();
            }
        }

        private IEnumerable<string> GetCatalogAssembliesLines(ComposableCatalog catalog)
        {
            yield return "Original assembly names:";
            foreach (string path in this.catalogAssemblyPaths.OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"\t{Path.GetFileNameWithoutExtension(path)} (\"{path}\")";
            }

            yield return string.Empty;

            yield return "Scanned assemblies with no discoverable parts:";
            var noncontributors = this.catalogAssemblyPaths.Select(Path.GetFileNameWithoutExtension)
                .Except(catalog.Parts.Select(p => p.TypeRef.AssemblyName.Name), StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();
            if (noncontributors.Any())
            {
                foreach (string path in noncontributors)
                {
                    yield return '\t' + path;
                }
            }
            else
            {
                yield return "\t- none -";
            }

            yield return string.Empty;
        }

        private void LogWarning(string code, string message)
        {
            if (!this.skipCodes.Value.Contains(code))
            {
                this.Log.LogWarning(null, code, null, null, 0, 0, 0, 0, message);
            }
        }

        /// <summary>
        /// Gets the full path to an assembly.
        /// </summary>
        /// <param name="taskItem">The assembly name or path to an assembly.</param>
        /// <returns>The full path to the assembly.</returns>
        private string GetMEFAssemblyFullPath(ITaskItem taskItem)
        {
            // So long as it doesn't obviously look like a path...
            if (!taskItem.ItemSpec.Contains(Path.DirectorySeparatorChar) &&
                !taskItem.ItemSpec.Contains(Path.AltDirectorySeparatorChar) &&
                !taskItem.ItemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !taskItem.ItemSpec.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // ... try parsing it as if it were an assembly name (so we can parse out just the simple name).
                    var assemblyName = new AssemblyName(taskItem.ItemSpec);
                    foreach (var searchDir in this.CatalogAssemblySearchPath)
                    {
                        string fullPath = Path.Combine(Path.GetFullPath(searchDir.ItemSpec), assemblyName.Name + ".dll");
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }

                        fullPath = Path.Combine(Path.GetFullPath(searchDir.ItemSpec), assemblyName.Name + ".exe");
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            // Fall back to assuming it's a path.
            return Path.GetFullPath(taskItem.GetMetadata("FullPath"));
        }

        private Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // apply any existing policy
            AssemblyName referenceName = new AssemblyName(AppDomain.CurrentDomain.ApplyPolicy(args.Name));

            string fileName = referenceName.Name + ".dll";
            Assembly? assm;

            // Look through user-specified assembly lists.
            foreach (var candidate in this.catalogAssemblyPaths.Concat(this.ReferenceAssemblies.Select(i => i.GetMetadata("FullPath"))))
            {
                if (string.Equals(referenceName.Name, Path.GetFileNameWithoutExtension(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    if (this.Probe(candidate, referenceName.Version, out assm))
                    {
                        return assm;
                    }
                }
            }

            string probingPath;
            foreach (var searchDir in this.CatalogAssemblySearchPath)
            {
                probingPath = Path.Combine(searchDir.GetMetadata("FullPath"), fileName);
                Debug.WriteLine($"Considering {probingPath} based on catalog assembly search path");
                if (this.Probe(probingPath, referenceName.Version, out assm))
                {
                    return assm;
                }
            }

            // look next to requesting assembly
            string? assemblyPath = args.RequestingAssembly?.Location;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                probingPath = Path.Combine(Path.GetDirectoryName(assemblyPath), fileName);
                Debug.WriteLine($"Considering {probingPath} based on RequestingAssembly");
                if (this.Probe(probingPath, referenceName.Version, out assm))
                {
                    return assm;
                }
            }

            // look next to the executing assembly
            assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                probingPath = Path.Combine(Path.GetDirectoryName(assemblyPath), fileName);

                Debug.WriteLine($"Considering {probingPath} based on ExecutingAssembly");
                if (this.Probe(probingPath, referenceName.Version, out assm))
                {
                    return assm;
                }
            }

            // look in AppDomain base directory
            probingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            Debug.WriteLine($"Considering {probingPath} based on BaseDirectory");
            if (this.Probe(probingPath, referenceName.Version, out assm))
            {
                return assm;
            }

            bool shouldLog;
            lock (this.loggedMissingAssemblyNames)
            {
                shouldLog = this.loggedMissingAssemblyNames.Add(args.Name);
            }

            if (shouldLog)
            {
                this.Log.LogMessage("\"{0}\" could not be found.", args.Name);
            }

            return null;
        }

        /// <summary>
        /// Considers a path to load for satisfying an assembly ref and loads it
        /// if the file exists and version is sufficient.
        /// </summary>
        /// <param name="filePath">Path to consider for load.</param>
        /// <param name="minimumVersion">Minimum version to consider.</param>
        /// <param name="assembly">loaded assembly.</param>
        /// <returns>true if assembly was loaded.</returns>
        private bool Probe(string filePath, Version minimumVersion, [NotNullWhen(true)] out Assembly? assembly)
        {
            if (File.Exists(filePath))
            {
                AssemblyName name = AssemblyName.GetAssemblyName(filePath);

                if (name.Version >= minimumVersion)
                {
                    try
                    {
                        assembly = Assembly.Load(name);
                        this.LogAssemblyLoad(assembly, filePath);
                        return true;
                    }
                    catch (BadImageFormatException)
                    {
                        // This happens for some reference assemblies that can only be loaded in a reflection-only context.
                        // But we're not allowed in an assembly resolve event to return reflection-only loaded assemblies.
                        // So just log it and return false to communicate the failure.
                        this.Log.LogMessage(MessageImportance.Low, "\"{0}\" failed to load from \"{1}\".", name, filePath);
                    }
                }
            }

            assembly = null;
            return false;
        }

        private static IEnumerable<string> GetCatalogErrorLines(ComposableCatalog catalog)
        {
            foreach (var error in catalog.DiscoveredParts.DiscoveryErrors)
            {
                yield return error.GetUserMessage();

                if (error.InnerException is ReflectionTypeLoadException reflectionTypeLoadException)
                {
                    // The LoaderExceptions have a tendency to have a lot of redundancy.
                    // So get just the unique set to report.
                    foreach (string uniqueMessage in reflectionTypeLoadException.LoaderExceptions.Select(e => e.Message).Distinct())
                    {
                        yield return "\t" + uniqueMessage;
                    }
                }
            }
        }

        private static IEnumerable<string> GetCompositionErrorLines(CompositionConfiguration composition)
        {
            int level = 1;
            foreach (var errors in composition.CompositionErrors)
            {
                yield return $"******************* Composition error level {level++} ********************";
                foreach (var error in errors)
                {
                    yield return error.Message;
                }

                yield return string.Empty;
            }
        }

        private void LogLines(string filePath, IEnumerable<string> lines, CancellationToken cancellationToken)
        {
            Requires.NotNullOrEmpty(filePath, nameof(filePath));
            Requires.NotNull(lines, nameof(lines));

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    foreach (string line in lines)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.WriteLine(line);
                    }

                    writer.Flush();
                }

                this.writtenFiles.Add(filePath);
            }
            catch (IOException ex)
            {
                // Don't emit an error code. This should rarely happen, and when it does it should call attention to folks
                // that they may have an overbuild / multiproc build problem.
                // Adding an error code would make it suppressible.
                this.Log.LogWarning("Unable to write log file: \"{0}\". {1}", filePath, ex.Message);
            }
        }

        private string GetLogFilePath(string partialFileName)
        {
            return Path.Combine(this.LogOutputPath, $"Microsoft.VisualStudio.Composition.AppHost.{partialFileName}.log");
        }

        private void LogAssemblyLoad(Assembly assembly, string expectedPath)
        {
            Requires.NotNull(assembly, nameof(assembly));

            lock (this.loggedAssemblies)
            {
                if (!this.loggedAssemblies.Add(assembly))
                {
                    return;
                }
            }

            if (string.Equals(expectedPath, assembly.Location, StringComparison.OrdinalIgnoreCase))
            {
                this.Log.LogMessage(MessageImportance.Low, "\"{0}\" loaded from \"{1}\"", assembly.FullName, assembly.Location);
            }
            else
            {
                this.Log.LogMessage(MessageImportance.Low, "\"{0}\" loaded from \"{1}\" after expecting it from \"{2}\"", assembly.FullName, assembly.Location, expectedPath);
            }
        }

        private class AssemblyLoader : IAssemblyLoader
        {
            private readonly Dictionary<string, string> assemblyNamesToPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly CreateComposition task;

            internal AssemblyLoader(CreateComposition task, IEnumerable<string> assemblyPaths)
            {
                Requires.NotNull(task, nameof(task));
                Requires.NotNull(assemblyPaths, nameof(assemblyPaths));

                this.task = task;

                foreach (string path in assemblyPaths)
                {
                    string assemblyName = Path.GetFileNameWithoutExtension(path);
                    if (!this.assemblyNamesToPaths.ContainsKey(assemblyName))
                    {
                        this.assemblyNamesToPaths.Add(assemblyName, path);
                    }
                }
            }

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

            public Assembly LoadAssembly(AssemblyName assemblyName)
            {
                if (string.IsNullOrEmpty(assemblyName.CodeBase) && this.assemblyNamesToPaths.TryGetValue(assemblyName.Name, out string path))
                {
                    assemblyName.CodeBase = path;
                }

                var assembly = Assembly.Load(assemblyName);
                this.task.LogAssemblyLoad(assembly, assembly.Location);
                return assembly;
            }
        }
    }
}
