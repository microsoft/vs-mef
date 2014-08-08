namespace Microsoft.VisualStudio.Composition.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class CreateComposition : AppDomainIsolatedTask, ICancelableTask
    {
        private readonly CancellationTokenSource cancellationSource = new CancellationTokenSource();

        public ITaskItem[] CatalogAssemblies { get; set; }

        [Required]
        public string CompositionCacheFile { get; set; }

        public string DgmlOutputPath { get; set; }

        public override bool Execute()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            try
            {
                var discovery = PartDiscovery.Combine(new AttributedPartDiscoveryV1(), new AttributedPartDiscovery());

                this.cancellationSource.Token.ThrowIfCancellationRequested();

                var parts = discovery.CreatePartsAsync(this.CatalogAssemblies.Select(item => item.ItemSpec)).GetAwaiter().GetResult();
                foreach (var error in parts.DiscoveryErrors)
                {
                    this.Log.LogWarningFromException(error);
                }

                this.cancellationSource.Token.ThrowIfCancellationRequested();
                var catalog = ComposableCatalog.Create(parts.Parts);
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
                    runtimeCache.SaveAsync(runtime, cacheStream, cancellationSource.Token).GetAwaiter().GetResult();
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
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }

            return !this.Log.HasLoggedErrors;
        }

        private static readonly string[] AssembliesToResolve = new string[]
        {
            "System.Composition.AttributedModel",
            "Validation",
            "System.Collections.Immutable",
        };

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
