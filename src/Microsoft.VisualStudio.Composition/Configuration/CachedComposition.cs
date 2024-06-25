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

    public class CachedComposition : ICompositionCacheManager, IRuntimeCompositionCacheManager
    {
        private static readonly Encoding TextEncoding = Encoding.UTF8;

        public Task SaveAsync(CompositionConfiguration configuration, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanWrite, "cacheStream", Strings.WritableStreamRequired);

            return Task.Run(async delegate
            {
                var compositionRuntime = RuntimeComposition.CreateRuntimeComposition(configuration);
                await this.SaveAsync(compositionRuntime, cacheStream, cancellationToken).ConfigureAwait(false);
            });
        }

        public Task SaveAsync(RuntimeComposition composition, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(composition, nameof(composition));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanWrite, "cacheStream", Strings.WritableStreamRequired);

            return Task.Run(() =>
            {
                var context = new MessagePackSerializerContext(StandardResolverAllowPrivate.Instance, composition.Resolver);
                MessagePackSerializer.Serialize(cacheStream, composition, context, cancellationToken);
            });
        }

        public async Task<RuntimeComposition> LoadRuntimeCompositionAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanRead, "cacheStream", Strings.ReadableStreamRequired);
            Requires.NotNull(resolver, nameof(resolver));

            var context = new MessagePackSerializerContext(StandardResolverAllowPrivate.Instance, resolver);
            return await MessagePackSerializer.DeserializeAsync<RuntimeComposition>(cacheStream, context, cancellationToken);
        }

        public async Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            var runtimeComposition = await this.LoadRuntimeCompositionAsync(cacheStream, resolver, cancellationToken).ConfigureAwait(false);
            return runtimeComposition.CreateExportProviderFactory();
        }
    }
}
