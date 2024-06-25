// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;

    public class CachedCatalog
    {
        protected static readonly Encoding TextEncoding = Encoding.UTF8;

        public async Task SaveAsync(ComposableCatalog catalog, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(catalog, nameof(catalog));
            Requires.NotNull(cacheStream, nameof(cacheStream));

            MessagePackSerializerContext context = new(catalog.Resolver);
            await MessagePackSerializer.SerializeAsync(cacheStream, catalog, context.DefaultOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ComposableCatalog> LoadAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.NotNull(resolver, nameof(resolver));

            MessagePackSerializerContext context = new(resolver);
            return await MessagePackSerializer.DeserializeAsync<ComposableCatalog>(cacheStream, context.DefaultOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}
