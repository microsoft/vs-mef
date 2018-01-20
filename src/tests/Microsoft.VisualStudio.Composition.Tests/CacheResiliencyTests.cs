// Copyright (c) Microsoft. All rights reserved.

#if DESKTOP

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Composition.Reflection;
    using Microsoft.VisualStudio.Composition.AppDomainTests;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;

    [Trait("AppDomains", "true")]
    public class CacheResiliencyTests
    {
        private ICompositionCacheManager cacheManager;

        public CacheResiliencyTests()
        {
            this.cacheManager = new CachedComposition();
        }

        [Fact]
        public void CacheStaleFromRecompiledAssembly()
        {
            // These are our two nearly-identical assemblies, but which are expected to have non-equivalent metadata tables,
            // which simulates the scenario of a cache being created with one assembly, then the cache is reused later after
            // that assembly has been re-compiled (possibly with source changes) such that its metadata table has changed,
            // making fast metadata token based reflection dangerous.
            string pathA = Path.Combine(Environment.CurrentDirectory, @"..\net45\Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests.dll");
            string pathB = Path.Combine(Environment.CurrentDirectory, @"..\netstandard1.2\Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests.dll");

            var cacheStream = new MemoryStream();
            int originalMetadataToken;
            var appDomain = AppDomain.CreateDomain("Cache creator", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainDriver).Assembly.FullName, typeof(AppDomainDriver).FullName);
                driver.CreateCache(pathA, cacheStream, out originalMetadataToken);
                cacheStream.Position = 0;
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }

            appDomain = AppDomain.CreateDomain("Cache consumer", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                var driver = (AppDomainDriver)appDomain.CreateInstanceAndUnwrap(typeof(AppDomainDriver).Assembly.FullName, typeof(AppDomainDriver).FullName);
                driver.TestWithCache(cacheStream, pathB, originalMetadataToken);
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        private class CustomAssemblyLoader : IAssemblyLoader
        {
            private readonly string substitutedAssemblyPath;

            internal CustomAssemblyLoader(string substitutedAssemblyPath)
            {
                this.substitutedAssemblyPath = substitutedAssemblyPath;
            }

            public Assembly LoadAssembly(string assemblyFullName, string codeBasePath)
            {
                return Assembly.LoadFile(codeBasePath);
            }

            public Assembly LoadAssembly(AssemblyName assemblyName)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(this.substitutedAssemblyPath), assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return Assembly.LoadFile(this.substitutedAssemblyPath);
                }

                return Assembly.Load(assemblyName);
            }
        }

        private class AppDomainDriver : MarshalByRefObject
        {
            private readonly ICompositionCacheManager cacheManager = new CachedComposition();

            internal void CreateCache(string assemblyPath, Stream cacheStream, out int metadataToken)
            {
                Requires.NotNullOrEmpty(assemblyPath, nameof(assemblyPath));

                var resolver = new Resolver(new CustomAssemblyLoader(assemblyPath));
                var discovery = PartDiscovery.Combine(
                    new AttributedPartDiscovery(resolver, isNonPublicSupported: true),
                    new AttributedPartDiscoveryV1(resolver));
                var discoveredParts = discovery.CreatePartsAsync(new[] { assemblyPath }).GetAwaiter().GetResult();
                var catalog = ComposableCatalog.Create(resolver)
                    .AddParts(discoveredParts);
                var configuration = CompositionConfiguration.Create(catalog);
                this.cacheManager.SaveAsync(configuration, cacheStream).GetAwaiter().GetResult();
                metadataToken = GetMetadataTokenForDefaultCtor(catalog.Parts.Single(p => p.TypeRef.FullName == typeof(DiscoverablePart1).FullName).Type);
            }

            internal void TestWithCache(Stream cacheStream, string substitutedAssemblyPath, int oldMetadataToken)
            {
                var resolver = new Resolver(new CustomAssemblyLoader(substitutedAssemblyPath));
                var exportProviderFactory = this.cacheManager.LoadExportProviderFactoryAsync(cacheStream, resolver).GetAwaiter().GetResult();
                var exportProvider = exportProviderFactory.CreateExportProvider();

                // Avoid using GetExportedValue<T> since the default load context will still pick up the original assembly,
                // and the cast will fail.
                var importDefinition = new ImportDefinition(
                    typeof(DiscoverablePart1).FullName,
                    ImportCardinality.ExactlyOne,
                    ImmutableDictionary<string, object>.Empty,
                    ImmutableList<IImportSatisfiabilityConstraint>.Empty);
                var export = exportProvider.GetExports(importDefinition).Single();
                AssertEx.NotNull(export.Value);
                AssertEx.Equal(typeof(DiscoverablePart1).FullName, export.Value.GetType().FullName);

                // Validate that we loaded the substituted assembly and that the metadata token is different (to assert we're testing something useful)
                AssertEx.Equal(substitutedAssemblyPath, export.Value.GetType().Assembly.Location);
                AssertEx.NotEqual(oldMetadataToken, GetMetadataTokenForDefaultCtor(export.Value));
            }

            private static int GetMetadataTokenForDefaultCtor(Type type) => type?.GetConstructor(Type.EmptyTypes).MetadataToken ?? throw new ArgumentNullException(nameof(type));

            private static int GetMetadataTokenForDefaultCtor(object value) => value?.GetType().GetConstructor(Type.EmptyTypes).MetadataToken ?? throw new ArgumentNullException(nameof(value));
        }
    }
}

#endif
