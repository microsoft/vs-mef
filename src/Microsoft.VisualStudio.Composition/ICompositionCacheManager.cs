namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Reflection;

    public interface ICompositionCacheManager
    {
        Task SaveAsync(CompositionConfiguration configuration, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken));

        Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, MyResolver resolver, CancellationToken cancellationToken = default(CancellationToken));
    }
}
