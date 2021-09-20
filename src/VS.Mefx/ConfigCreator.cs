﻿namespace VS.Mefx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition;

    /// <summary>
    /// Class to store the catalog and config information for input assemblies.
    /// </summary>
    internal class ConfigCreator
    {
        /// <summary>
        /// List of file extensions that are considered valid when trying to find input files.
        /// </summary>
        /// <remarks>
        /// Ensure that the cache extension remains last since the program operates on that assumption.
        /// </remarks>
        private static readonly string[] ValidExtensions = { "dll", "exe", "cache" };

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigCreator"/> class.
        /// </summary>
        /// <param name="options">The arguments inputted by the user.</param>
        public ConfigCreator(CLIOptions options)
        {
            this.AssemblyPaths = new List<string>();
            this.CachePaths = new List<string>();
            this.PartInformation = new Dictionary<string, ComposablePartDefinition>();
            this.Writer = options.Writer;

            // Add all the files in the input argument to the list of paths
            string currentFolder = Directory.GetCurrentDirectory();
            IEnumerable<string> files = options.Files;
            if (files != null)
            {
                foreach (string file in files)
                {
                    if (!this.AddFile(currentFolder, file))
                    {
                        string missingFileMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.MissingFileMessage,
                            file);
                        this.Writer.WriteLine(missingFileMessage);
                    }
                }
            }

            // Add all the valid files in the input folders to the list of paths
            IEnumerable<string> folders = options.Folders;
            if (folders != null)
            {
                foreach (string folder in folders)
                {
                    string folderPath = Path.GetFullPath(Path.Combine(currentFolder, folder));
                    if (Directory.Exists(folderPath))
                    {
                        this.SearchFolder(folderPath);
                    }
                    else
                    {
                        string missingFolderMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.MissingFolderMessage,
                            folder);
                        this.Writer.WriteLine(missingFolderMessage);
                    }
                }
            }

            this.OutputCacheFile = options.CacheFile;
        }

        /// <summary>
        /// Gets or sets the writer to use when writing output to the user.
        /// </summary>
        private TextWriter Writer { get; set; }

        /// <summary>
        /// Gets the catalog that stores information about the imported parts.
        /// </summary>
        public ComposableCatalog? Catalog { get; private set; }

        /// <summary>
        /// Gets configuration information associated with the imported parts.
        /// </summary>
        public CompositionConfiguration? Config { get; private set; }

        /// <summary>
        /// Gets or sets a dictionary storing parts indexed by thier parts name for easy lookup.
        /// </summary>
        public Dictionary<string, ComposablePartDefinition>? PartInformation { get; set; }

        /// <summary>
        /// Gets or sets the path of the cache file to store the processed parts.
        /// </summary>
        private string? OutputCacheFile { get; set; }

        /// <summary>
        /// Gets or sets the paths to the assembly files we want to read.
        /// </summary>
        private List<string> AssemblyPaths { get; set; }

        /// <summary>
        /// Gets or sets the paths to the cache files we want to read.
        /// </summary>
        private List<string> CachePaths { get; set; }

        /// <summary>
        /// Method to get the details about a part, i.e. the part Definition, given its name.
        /// </summary>
        /// <param name="partName"> The name of the part we want to get details about.</param>
        /// <returns><see cref="ComposablePartDefinition"/> associated with the given part if it is
        /// present in the catalog and null otherwise.</returns>
        public ComposablePartDefinition GetPart(string partName)
        {
            if (!this.PartInformation.ContainsKey(partName))
            {
                return null;
            }

            return this.PartInformation[partName];
        }

        /// <summary>
        /// Method to intialize the catalog and configuration objects from the input files.
        /// </summary>
        /// <returns>A Task object when all the assembly have between loaded in and configured.</returns>
        public async Task Initialize()
        {
            var customLoader = new CustomAssemblyLoader(this.Writer);
            Resolver customResolver = new(customLoader);
            var nugetDiscover = new AttributedPartDiscovery(customResolver, isNonPublicSupported: true);
            var netDiscover = new AttributedPartDiscoveryV1(customResolver);
            PartDiscovery discovery = PartDiscovery.Combine(customResolver, netDiscover, nugetDiscover);
            if (this.AssemblyPaths.Count() > 0)
            {
                var parts = await discovery.CreatePartsAsync(this.AssemblyPaths);
                this.Catalog = ComposableCatalog.Create(discovery.Resolver).AddParts(parts);
            }

            if (this.CachePaths.Count > 0)
            {
                await this.ReadCacheFiles(discovery);
            }

            this.PrintDiscoveryErrors();
            if (this.Catalog != null)
            {
                this.Config = CompositionConfiguration.Create(this.Catalog);

                // Add all the parts to the dictionary for lookup
                foreach (ComposablePartDefinition part in this.Catalog.Parts)
                {
                    string partName = part.Type.FullName;
                    if (!this.PartInformation.ContainsKey(partName))
                    {
                        this.PartInformation.Add(partName, part);
                    }
                }

                if (this.OutputCacheFile.Length > 0)
                {
                    await this.SaveToCache();
                }
            }
        }

        /// <summary>
        /// Method to add a given file to the list of all the assembly paths.
        /// A file is added to the list of path if it contains a valid extension and actually exists.
        /// </summary>
        /// <param name="folderPath">Path to the folder where the file is located.</param>
        /// <param name="fileName">Name of file we want to read parts from.</param>
        /// <returns> A boolean indicating if the file was added to the list of paths.</returns>
        private bool AddFile(string folderPath, string fileName)
        {
            fileName = fileName.Trim();
            int extensionIndex = fileName.LastIndexOf('.');
            bool isSucessful = false;
            if (extensionIndex >= 0)
            {
                string extension = fileName.Substring(extensionIndex + 1);
                if (ValidExtensions.Contains(extension))
                {
                    string fullPath = Path.GetFullPath(Path.Combine(folderPath, fileName));
                    if (File.Exists(fullPath))
                    {
                        bool isCacheFile = extension.Equals(ValidExtensions[ValidExtensions.Length - 1]);
                        if (isCacheFile)
                        {
                            this.CachePaths.Add(fullPath);
                        }
                        else
                        {
                            this.AssemblyPaths.Add(fullPath);
                        }

                        isSucessful = true;
                    }
                }
            }

            return isSucessful;
        }

        /// <summary>
        /// Method to add valid files from the current folder and its subfolders to the list of paths.
        /// </summary>
        /// <param name="currentPath">The complete path to the folder we want to add files from.</param>
        private void SearchFolder(string currentPath)
        {
            DirectoryInfo currentDir = new DirectoryInfo(currentPath);
            var files = currentDir.EnumerateFiles();
            foreach (var file in files)
            {
                string name = file.Name;
                this.AddFile(currentPath, name);
            }

            IEnumerable<DirectoryInfo> subFolders = currentDir.EnumerateDirectories();
            if (subFolders.Count() > 0)
            {
                foreach (DirectoryInfo subFolder in subFolders)
                {
                    this.SearchFolder(subFolder.FullName);
                }
            }
        }

        /// <summary>
        /// Method to read the input parts stored in cache files and add them to the existing Catalog.
        /// </summary>
        /// <param name="discovery">Part Discovery object to use when discovering parts in assembly.</param>
        private async Task ReadCacheFiles(PartDiscovery discovery)
        {
            foreach (string filePath in this.CachePaths)
            {
                try
                {
                    FileStream inputStream = File.OpenRead(filePath);
                    CachedCatalog catalogReader = new CachedCatalog();
                    ComposableCatalog cacheParts = await catalogReader.LoadAsync(inputStream, discovery.Resolver);
                    if (this.Catalog == null)
                    {
                        this.Catalog = cacheParts;
                    }
                    else
                    {
                        this.Catalog = this.Catalog.AddCatalog(cacheParts);
                    }
                }
                catch (Exception error)
                {
                    string cacheReadError = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ErrorMessage,
                        error.Message);
                    this.Writer.WriteLine(cacheReadError);
                }
            }
        }

        /// <summary>
        /// Method to store the parts read from the input files into a cache for future use.
        /// </summary>
        private async Task SaveToCache()
        {
            string fileName = this.OutputCacheFile.Trim();
            int extensionIndex = fileName.LastIndexOf('.');
            string cacheExtension = ValidExtensions[ValidExtensions.Length - 1];
            if (extensionIndex >= 0 && fileName.Substring(extensionIndex + 1).Equals(cacheExtension))
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                filePath = Path.GetFullPath(filePath);
                try
                {
                    CachedCatalog cacheWriter = new CachedCatalog();
                    using (var fileWriter = File.Create(filePath))
                    {
                        await cacheWriter.SaveAsync(this.Catalog, fileWriter);
                        string cacheSaved = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.SavedCacheMessage,
                        fileName);
                        this.Writer.WriteLine(cacheSaved);
                    }
                }
                catch (Exception error)
                {
                    string cacheSaveError = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ErrorMessage,
                        error.Message);
                    this.Writer.WriteLine(cacheSaveError);
                }
            }
            else
            {
                string invalidFile = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidFileName,
                        fileName);
                this.Writer.WriteLine(invalidFile);
            }

            this.Writer.WriteLine();
        }

        /// <summary>
        /// Method to print any discovery errors encountered during catalog creation.
        /// </summary>
        private void PrintDiscoveryErrors()
        {
            if (this.Catalog != null)
            {
                var discoveryErrors = this.Catalog.DiscoveredParts.DiscoveryErrors;
                if (discoveryErrors.Count() > 0)
                {
                    this.Writer.WriteLine(Strings.DiscoveryErrors);
                    discoveryErrors.ForEach(error => this.Writer.WriteLine(error));
                }
            }
        }

        private class CustomAssemblyLoader : IAssemblyLoader
        {
            public CustomAssemblyLoader(TextWriter writer)
            {
                this.Context = new AssemblyLoadContext("Mefx");
                this.LoadAssemblies = new Dictionary<string, Assembly>();
                this.Writer = writer;
            }

            /// <summary>
            /// Gets or sets a Load Context to see when loading the assemblies into Mefx.
            /// </summary>
            private AssemblyLoadContext Context { get; set; }

            /// <summary>
            /// Gets or sets an dictionary to keep track of the loaded dictionaries.
            /// </summary>
            private Dictionary<string, Assembly> LoadAssemblies { get; set; }

            /// <summary>
            /// Gets or sets a writer to use when outputting information to the user.
            /// </summary>
            private TextWriter Writer { get; set; }

            /// <summary>
            /// Loads the assembly with the specified name and path.
            /// </summary>
            /// <param name="assemblyFullName">The name of the assembly to load.</param>
            /// <param name="codeBasePath">The path of the assembly to load.</param>
            /// <returns>The loaded assembly at the given codePath.</returns>
            public Assembly LoadAssembly(string assemblyFullName, string? codeBasePath)
            {
                // Try to read using the path first and use assemblyName as backup
                if (codeBasePath != null)
                {
                    return this.LoadUsingPath(codeBasePath);
                }
                else
                {
                    return this.LoadUsingName(new AssemblyName(assemblyFullName));
                }
            }

            /// <summary>
            /// Loads the assembly with the assemblyName.
            /// </summary>
            /// <param name="assemblyName">The <see cref="AssemblyName"/> to read the assembly for.</param>
            /// <returns>The loaded assembly with the given assemblyName.</returns>
            public Assembly LoadAssembly(AssemblyName assemblyName)
            {
                // Try to read using the path first and use assemblyName as backup
                if (assemblyName.CodeBase != null)
                {
                    return this.LoadUsingPath(assemblyName.CodeBase);
                }
                else
                {
                    return this.LoadUsingName(assemblyName);
                }
            }

            /// <summary>
            /// Method to load an assembly using the given path name.
            /// </summary>
            /// <param name="path">A string representing the path to the assembly.</param>
            /// <returns>The assembly at the specified input path.</returns>
            private Assembly LoadUsingPath(string path)
            {
                path = path.Trim();

                // this.Writer.WriteLine("LoadUsingPath with path: " + path);
                lock (this.LoadAssemblies)
                {
                    if (this.LoadAssemblies.ContainsKey(path))
                    {
                        return this.LoadAssemblies[path];
                    }

                    Assembly current = this.Context.LoadFromAssemblyPath(path);
                    this.LoadAssemblies.Add(path, current);
                    return current;
                }
            }

            /// <summary>
            /// Method to load an assembly using the specified assembly name.
            /// </summary>
            /// <param name="assemblyInfo">The <see cref="AssemblyName"/> to load.</param>
            /// <returns>The assembly that matches the specified assembly name.</returns>
            private Assembly LoadUsingName(AssemblyName assemblyInfo)
            {
                string assemblyName = assemblyInfo.FullName.Trim();

                // this.Writer.WriteLine("LoadUsingName with assemblyName: " + assemblyName);
                lock (this.LoadAssemblies)
                {
                    if (this.LoadAssemblies.ContainsKey(assemblyName))
                    {
                        return this.LoadAssemblies[assemblyName];
                    }

                    Assembly current = this.Context.LoadFromAssemblyName(assemblyInfo);
                    this.LoadAssemblies.Add(assemblyName, current);
                    return current;
                }
            }

        }
    }
}
