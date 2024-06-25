// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MessagePack;

    public class CachedComposition : ICompositionCacheManager, IRuntimeCompositionCacheManager
    {
        private static readonly Encoding TextEncoding = Encoding.UTF8;

        public async Task SaveAsync(CompositionConfiguration configuration, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanWrite, "cacheStream", Strings.WritableStreamRequired);

            RuntimeComposition compositionRuntime = RuntimeComposition.CreateRuntimeComposition(configuration);
            await this.SaveAsync(compositionRuntime, cacheStream, cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveAsync(RuntimeComposition composition, Stream cacheStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(composition, nameof(composition));
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanWrite, "cacheStream", Strings.WritableStreamRequired);

            MessagePackSerializerContext context = new(composition.Resolver);
            await MessagePackSerializer.SerializeAsync(cacheStream, composition, context.DefaultOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task<RuntimeComposition> LoadRuntimeCompositionAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(cacheStream, nameof(cacheStream));
            Requires.Argument(cacheStream.CanRead, "cacheStream", Strings.ReadableStreamRequired);
            Requires.NotNull(resolver, nameof(resolver));

            MessagePackSerializerContext context = new(resolver);
            return await MessagePackSerializer.DeserializeAsync<RuntimeComposition>(cacheStream, context.DefaultOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IExportProviderFactory> LoadExportProviderFactoryAsync(Stream cacheStream, Resolver resolver, CancellationToken cancellationToken = default(CancellationToken))
        {
            RuntimeComposition runtimeComposition = await this.LoadRuntimeCompositionAsync(cacheStream, resolver, cancellationToken).ConfigureAwait(false);
            return runtimeComposition.CreateExportProviderFactory();
        }
    }
}
