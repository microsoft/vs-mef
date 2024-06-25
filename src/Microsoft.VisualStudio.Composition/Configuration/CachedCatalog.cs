// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;
    using MessagePack.Resolvers;

    public class CachedCatalog
    {
        protected static readonly Encoding TextEncoding = Encoding.UTF8;

        public Task SaveAsync(ComposableCatalog catalog, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(catalog, nameof(catalog));
            Requires.NotNull(cacheStream, nameof(cacheStream));

            return Task.Run(() =>
            {
                var context = new MessagePackSerializerContext(StandardResolverAllowPrivate.Instance, catalog.Resolver);
                MessagePackSerializer.Serialize(cacheStream, catalog, context, cancellationToken);
            });
        }

        public async Task<ComposableCatalog> LoadAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.NotNull(resolver, nameof(resolver));

            var context = new MessagePackSerializerContext(StandardResolverAllowPrivate.Instance, resolver);
            return await MessagePackSerializer.DeserializeAsync<ComposableCatalog>(cacheStream, context, cancellationToken);
        }
    }
}
