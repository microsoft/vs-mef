// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AppDomainTests;
    using Microsoft.VisualStudio.Composition.AppDomainTests2;
    using Xunit;
    using Xunit.Abstractions;

    public abstract class CacheAndReloadTests
    {
        private readonly ITestOutputHelper logger;
        private ICompositionCacheManager cacheManager;

        protected CacheAndReloadTests(ITestOutputHelper logger, ICompositionCacheManager cacheManager)
        {
            Requires.NotNull(cacheManager, nameof(cacheManager));
            this.logger = logger;
            this.cacheManager = cacheManager;
        }

        [Fact]
        public async Task CacheAndReload()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(new[] { TestUtilities.V2Discovery.CreatePart(typeof(SomeExport)) });
            var configuration = CompositionConfiguration.Create(catalog);
            var ms = new MemoryStream();
            await this.cacheManager.SaveAsync(configuration, ms);
            configuration = null;

            ms.Position = 0;
            var exportProviderFactory = await this.cacheManager.LoadExportProviderFactoryAsync(ms, TestUtilities.Resolver);
            var container = exportProviderFactory.CreateExportProvider();
            SomeExport export = container.GetExportedValue<SomeExport>();
            Assert.NotNull(export);
        }

        [Export]
        public class SomeExport { }
    }
}
