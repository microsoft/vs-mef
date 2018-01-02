// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AppHost
{
    using System;
    using System.Collections.Generic;
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
    using Reflection;

    public class CreateComposition : AppDomainIsolatedTask, ICancelableTask
    {
        private static readonly string[] AssembliesToResolve = new string[]
        {
            "System.Composition.AttributedModel",
            "Validation",
            "System.Collections.Immutable",
        };

        /// <summary>
        /// The source of the <see cref="CancellationToken" /> that is canceled when
        /// <see cref="ICancelableTask.Cancel" /> is invoked.
        /// </summary>
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>Gets a token that is canceled when MSBuild is requesting that we abort.</summary>
        public CancellationToken CancellationToken => this.cts.Token;

        public ITaskItem[] CatalogAssemblies { get; set; }

        [Required]
        public string CompositionCacheFile { get; set; }

        public string DgmlOutputPath { get; set; }

        /// <inheritdoc />
        public void Cancel() => this.cts.Cancel();

        public override bool Execute()
        {
            var resolver = Resolver.DefaultInstance;
            var discovery = PartDiscovery.Combine(new AttributedPartDiscoveryV1(resolver), new AttributedPartDiscovery(resolver, isNonPublicSupported: true));

            this.CancellationToken.ThrowIfCancellationRequested();

            var parts = discovery.CreatePartsAsync(this.CatalogAssemblies.Select(item => item.ItemSpec)).GetAwaiter().GetResult();
            foreach (var error in parts.DiscoveryErrors)
            {
                this.Log.LogWarningFromException(error);
            }

            this.CancellationToken.ThrowIfCancellationRequested();
            var catalog = ComposableCatalog.Create(resolver)
                .AddParts(parts.Parts);
            this.CancellationToken.ThrowIfCancellationRequested();
            var configuration = CompositionConfiguration.Create(catalog);

            if (!string.IsNullOrEmpty(this.DgmlOutputPath))
            {
                configuration.CreateDgml().Save(this.DgmlOutputPath);
            }

            this.CancellationToken.ThrowIfCancellationRequested();
            if (!configuration.CompositionErrors.IsEmpty)
            {
                foreach (var error in configuration.CompositionErrors.Peek())
                {
                    this.Log.LogError(error.Message);
                }

                return false;
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

            return !this.Log.HasLoggedErrors;
        }
    }
}
