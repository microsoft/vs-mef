// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.BenchmarkTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using Microsoft.VisualStudio.Composition.Tests;
    using static Microsoft.VisualStudio.Composition.Tests.AssemblyReferencingTests;

    public class CacheAndReloadBenchmarkHost
    {
        private ComposableCatalog catalog = TestUtilities.EmptyCatalog.AddParts([TestUtilities.V2Discovery.CreatePart(typeof(SomeExport))!]);
        private CachedComposition cacheManager = new CachedComposition();

        public async Task CacheAndReloadAsync()
        {
            var configuration = CompositionConfiguration.Create(this.catalog);
            var ms = new MemoryStream();
            await this.cacheManager.SaveAsync(configuration, ms);
            configuration = null;

            ms.Position = 0;
            var exportProviderFactory = await this.cacheManager.LoadExportProviderFactoryAsync(ms, TestUtilities.Resolver);
            var container = exportProviderFactory.CreateExportProvider();
            SomeExport export = container.GetExportedValue<SomeExport>();
        }
    }
}
