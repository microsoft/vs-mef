// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    /// <summary>
    /// Supports metadata views that are any type that <see cref="ImmutableDictionary{TKey, TValue}"/>
    /// could be assigned to, including <see cref="IDictionary{TKey, TValue}"/> and <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    internal class PassthroughMetadataViewProvider : IMetadataViewProvider
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(PassthroughMetadataViewProvider), 1001000, Resolver.DefaultInstance);

        internal static readonly IMetadataViewProvider Default = new PassthroughMetadataViewProvider();

        private PassthroughMetadataViewProvider()
        {
        }

        public bool IsMetadataViewSupported(Type metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));

            return metadataType.GetTypeInfo().IsAssignableFrom(typeof(IReadOnlyDictionary<string, object>).GetTypeInfo())
                || metadataType.GetTypeInfo().IsAssignableFrom(typeof(IDictionary<string, object>).GetTypeInfo());
        }

        public object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
        {
            Requires.NotNull(metadata, nameof(metadata));

            // This cast should work because our IsMetadataViewSupported method filters to those that do.
            return metadata;
        }
    }
}
