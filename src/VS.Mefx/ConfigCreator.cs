// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace VS.Mefx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
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
        /// List of substrings to look for in that should be ignored.
        /// </summary>
        private static readonly string[] InvalidFileStrings = { "System." };

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigCreator"/> class.
        /// </summary>
        /// <param name="options">The arguments inputted by the user.</param>
        internal ConfigCreator(CLIOptions options)
        {
            this.Options = options;

            // Add all the files in the input argument to the list of paths
            string currentFolder = Directory.GetCurrentDirectory();
            if (this.Options.Files != null)
            {
                IEnumerable<string> files = this.Options.Files;
                foreach (string file in files)
                {
                    if (!this.AddFile(currentFolder, file))
                    {
                        string missingFileMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.MissingFileMessage,
                            file);
                        this.Options.ErrorWriter.WriteLine(missingFileMessage);
                    }
                }
            }

            // Add all the valid files in the input folders to the list of paths
            if (this.Options.Folders != null)
            {
                IEnumerable<string> folders = this.Options.Folders;
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
                        this.Options.ErrorWriter.WriteLine(missingFolderMessage);
                    }
                }
            }

            this.OutputCacheFile = options.CacheFile;
        }

        /// <summary>
        /// Gets or sets the command line arguments specified by the user.
        /// </summary>
        private CLIOptions Options { get; set; }

        /// <summary>
        /// Gets the catalog that stores information about the imported parts.
        /// </summary>
        internal ComposableCatalog? Catalog { get; private set; }

        /// <summary>
        /// Gets configuration information associated with the imported parts.
        /// </summary>
        internal CompositionConfiguration? Config { get; private set; }

        /// <summary>
        /// Gets a dictionary storing parts indexed by thier parts name for easy lookup.
        /// </summary>
        internal Dictionary<string, ComposablePartDefinition> PartInformation { get; } = new();

        /// <summary>
        /// Gets or sets the path of the cache file to store the processed parts.
        /// </summary>
        internal string? OutputCacheFile { get; set; }

        /// <summary>
        /// Gets the paths to the assembly files we want to read.
        /// </summary>
        internal List<string> AssemblyPaths { get; } = new List<string>();

        /// <summary>
        /// Gets the paths to the cache files we want to read.
        /// </summary>
        internal List<string> CachePaths { get; } = new List<string>();

        /// <summary>
        /// Method to get the details about a part, i.e. the part Definition, given its name.
        /// </summary>
        /// <param name="partName"> The name of the part we want to get details about.</param>
        /// <returns>
        /// <see cref="ComposablePartDefinition"/> associated with the given part if it is
        /// present in the catalog and null otherwise.
        /// </returns>
        internal ComposablePartDefinition? GetPart(string partName)
        {
            this.PartInformation.TryGetValue(partName, out ComposablePartDefinition? partDefinition);
            return partDefinition;
        }

        /// <summary>
        /// Method to intialize the catalog and configuration objects from the input files.
        /// </summary>
        /// <returns>A Task object when all the assembly have between loaded in and configured.</returns>
        internal async Task InitializeAsync()
        {
            Resolver customResolver = Resolver.DefaultInstance;
            var nugetDiscover = new AttributedPartDiscovery(customResolver, isNonPublicSupported: true);
            var netDiscover = new AttributedPartDiscoveryV1(customResolver);
            PartDiscovery discovery = PartDiscovery.Combine(customResolver, netDiscover, nugetDiscover);

            if (this.AssemblyPaths.Any())
            {
                var parts = await discovery.CreatePartsAsync(this.AssemblyPaths);
                this.Catalog = ComposableCatalog.Create(discovery.Resolver).AddParts(parts);
            }

            if (this.CachePaths.Count > 0)
            {
                await this.ReadCacheFilesAsync(discovery);
            }

            this.PrintDiscoveryErrors();
            if (this.Catalog != null)
            {
                this.Config = CompositionConfiguration.Create(this.Catalog);

                // Add all the parts to the dictionary for lookup
                foreach (ComposablePartDefinition part in this.Catalog.Parts)
                {
                    string partName = part.Type.FullName!;
                    if (!this.PartInformation.ContainsKey(partName))
                    {
                        this.PartInformation.Add(partName, part);
                    }
                }

                await this.SaveToCacheAsync();
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

            if (extensionIndex < 0 || extensionIndex >= fileName.Length)
            {
                return false;
            }

            string extension = fileName.Substring(extensionIndex + 1);
            if (!ValidExtensions.Contains(extension))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(folderPath, fileName));
            if (!File.Exists(fullPath))
            {
                return false;
            }

            string fileNameWithoutExt = fileName.Substring(0, extensionIndex);
            for (int i = 0; i < InvalidFileStrings.Length; i++)
            {
                if (fileNameWithoutExt.Contains(InvalidFileStrings[i]))
                {
                    return false;
                }
            }

            bool isCacheFile = extension.Equals(ValidExtensions[ValidExtensions.Length - 1]);
            if (isCacheFile)
            {
                this.CachePaths.Add(fullPath);
            }
            else
            {
                this.AssemblyPaths.Add(fullPath);
            }

            return true;
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
            if (subFolders.Any())
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
        private async Task ReadCacheFilesAsync(PartDiscovery discovery)
        {
            foreach (string filePath in this.CachePaths)
            {
                try
                {
                    using (FileStream inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
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
                }
                catch (Exception error)
                {
                    string cacheReadError = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ErrorMessage,
                        error.Message);
                    this.Options.ErrorWriter.WriteLine(cacheReadError);
                }
            }
        }

        /// <summary>
        /// Method to store the parts read from the input files into a cache for future use.
        /// </summary>
        private async Task SaveToCacheAsync()
        {
            if (this.Catalog == null ||
                this.OutputCacheFile == null ||
                this.OutputCacheFile.Length == 0)
            {
                return;
            }

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
                    using (var fileWriter = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write))
                    {
                        await cacheWriter.SaveAsync(this.Catalog, fileWriter);
                        string cacheSaved = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.SavedCacheMessage,
                        fileName);
                        this.Options.Writer.WriteLine(cacheSaved);
                    }
                }
                catch (Exception error)
                {
                    string cacheSaveError = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ErrorMessage,
                        error.Message);
                    this.Options.ErrorWriter.WriteLine(cacheSaveError);
                }
            }
            else
            {
                string invalidFile = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidFileName,
                        fileName);
                this.Options.ErrorWriter.WriteLine(invalidFile);
            }

            this.Options.Writer.WriteLine();
        }

        /// <summary>
        /// Method to print any discovery errors encountered during catalog creation.
        /// </summary>
        private void PrintDiscoveryErrors()
        {
            if (this.Catalog != null)
            {
                var discoveryErrors = this.Catalog.DiscoveredParts.DiscoveryErrors;
                if (discoveryErrors.Any())
                {
                    this.Options.ErrorWriter.WriteLine(Strings.DiscoveryErrors);
                    discoveryErrors.ForEach(error => this.Options.ErrorWriter.WriteLine(error));
                }
            }
        }
    }
}
