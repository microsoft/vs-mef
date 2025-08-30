// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Samples.Hosting
{
    using System.Reflection;
    using Microsoft.VisualStudio.Composition;

    namespace Extensible
    {
        class Program
        {
            async Task Snippet()
            {
                #region Extensible
                // Prepare part discovery to support both flavors of MEF attributes.
                var discovery = PartDiscovery.Combine(
                    new AttributedPartDiscovery(Resolver.DefaultInstance), // "NuGet MEF" attributes (Microsoft.Composition)
                    new AttributedPartDiscoveryV1(Resolver.DefaultInstance)); // ".NET MEF" attributes (System.ComponentModel.Composition)

                // Build up a catalog of MEF parts
                var catalog = ComposableCatalog.Create(Resolver.DefaultInstance)
                    .AddParts(await discovery.CreatePartsAsync(Assembly.GetExecutingAssembly()))
                    .WithCompositionService(); // Makes an ICompositionService export available to MEF parts to import

                // Assemble the parts into a valid graph.
                var config = CompositionConfiguration.Create(catalog);

                // Prepare an ExportProvider factory based on this graph.
                var epf = config.CreateExportProviderFactory();

                // Create an export provider, which represents a unique container of values.
                // You can create as many of these as you want, but typically an app needs just one.
                var exportProvider = epf.CreateExportProvider();

                // Obtain our first exported value
                var program = exportProvider.GetExportedValue<Program>();
                #endregion
            }
        }
    }

    namespace DirectoryCatalog
    {
        #region DirectoryCatalog
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Reflection;
        using Microsoft.VisualStudio.Composition;

        internal class MefHosting
        {
            /// <summary>
            /// The MEF discovery module to use (which finds both MEFv1 and MEFv2 parts).
            /// </summary>
            private readonly PartDiscovery discoverer = PartDiscovery.Combine(
                new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true),
                new AttributedPartDiscoveryV1(Resolver.DefaultInstance));

            /// <summary>
            /// Gets the names of assemblies that belong to the application .exe folder.
            /// </summary>
            /// <returns>A list of assembly names.</returns>
            private static IEnumerable<string> GetAssemblyNames()
            {
                string directoryToSearch = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                foreach (string file in Directory.EnumerateFiles(directoryToSearch, "*.dll"))
                {
                    string? assemblyFullName = null;
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(file);
                        if (assemblyName is not null)
                        {
                            assemblyFullName = assemblyName.FullName;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    if (assemblyFullName is not null)
                    {
                        yield return assemblyFullName;
                    }
                }
            }

            /// <summary>
            /// Creates a catalog with all the assemblies from the application .exe's directory.
            /// </summary>
            /// <returns>A task whose result is the <see cref="ComposableCatalog"/>.</returns>
            private async Task<ComposableCatalog> CreateProductCatalogAsync()
            {
                var assemblyNames = GetAssemblyNames();
                var assemblies = assemblyNames.Select(Assembly.Load);
                var discoveredParts = await this.discoverer.CreatePartsAsync(assemblies);
                var catalog = ComposableCatalog.Create(Resolver.DefaultInstance)
                    .AddParts(discoveredParts);
                return catalog;
            }
        }
        #endregion
    }
}
