namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRuntimeCompositionCacheManager
    {
        Task SaveAsync(RuntimeComposition composition, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken));

        Task<RuntimeComposition> LoadRuntimeCompositionAsync(Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken));
    }
}
