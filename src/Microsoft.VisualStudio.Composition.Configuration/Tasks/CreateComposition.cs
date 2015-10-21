namespace Microsoft.VisualStudio.Composition.Tasks
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

        private readonly CancellationTokenSource cancellationSource = new CancellationTokenSource();

        public ITaskItem[] CatalogAssemblies { get; set; }

        [Required]
        public string CompositionCacheFile { get; set; }

        public string DgmlOutputPath { get; set; }

        public override bool Execute()
        {
            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
            try
            {
                var resolver = Resolver.DefaultInstance;
                var discovery = PartDiscovery.Combine(new AttributedPartDiscoveryV1(resolver), new AttributedPartDiscovery(resolver));

                this.cancellationSource.Token.ThrowIfCancellationRequested();

                var parts = discovery.CreatePartsAsync(this.CatalogAssemblies.Select(item => item.ItemSpec)).GetAwaiter().GetResult();
                foreach (var error in parts.DiscoveryErrors)
                {
                    this.Log.LogWarningFromException(error);
                }

                this.cancellationSource.Token.ThrowIfCancellationRequested();
                var catalog = ComposableCatalog.Create(resolver)
                    .AddParts(parts.Parts)
                    .WithDesktopSupport();
                this.cancellationSource.Token.ThrowIfCancellationRequested();
                var configuration = CompositionConfiguration.Create(catalog);

                if (!string.IsNullOrEmpty(this.DgmlOutputPath))
                {
                    configuration.CreateDgml().Save(this.DgmlOutputPath);
                }

                this.cancellationSource.Token.ThrowIfCancellationRequested();
                if (!configuration.CompositionErrors.IsEmpty)
                {
                    foreach (var error in configuration.CompositionErrors.Peek())
                    {
                        this.Log.LogError(error.Message);
                    }

                    return false;
                }

                this.cancellationSource.Token.ThrowIfCancellationRequested();

                string cachePath = Path.GetFullPath(this.CompositionCacheFile);
                this.Log.LogMessage("Producing IoC container \"{0}\"", cachePath);
                using (var cacheStream = File.Open(cachePath, FileMode.Create))
                {
                    this.cancellationSource.Token.ThrowIfCancellationRequested();
                    var runtime = RuntimeComposition.CreateRuntimeComposition(configuration);
                    this.cancellationSource.Token.ThrowIfCancellationRequested();
                    var runtimeCache = new CachedComposition();
                    runtimeCache.SaveAsync(runtime, cacheStream, this.cancellationSource.Token).GetAwaiter().GetResult();
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception inner in ex.Flatten().InnerExceptions)
                {
                    this.Log.LogError(inner.GetUserMessage());
                }
            }
            catch (Exception ex)
            {
                this.Log.LogError(ex.GetUserMessage());
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= this.CurrentDomain_AssemblyResolve;
            }

            return !this.Log.HasLoggedErrors;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFile",
            Justification = "Resolves not finding an assembly in the current domain by looking elsewhere, so LoadFile is needed")]
        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (Array.IndexOf(AssembliesToResolve, name.Name) >= 0)
            {
                string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(basePath, name.Name + ".dll");
                return Assembly.LoadFile(path);
            }

            return null;
        }

        public void Cancel()
        {
            this.cancellationSource.Cancel();
        }
    }
}
