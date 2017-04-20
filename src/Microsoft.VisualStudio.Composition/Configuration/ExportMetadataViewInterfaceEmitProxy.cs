// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using Reflection;

    internal static class ExportMetadataViewInterfaceEmitProxy
    {
        private static readonly ComposablePartDefinition ProxySupportPartDefinition = new AttributedPartDiscovery(Resolver.DefaultInstance).CreatePart(typeof(MetadataViewProxyProvider));

        /// <summary>
        /// Adds support for queries to <see cref="ExportProvider.GetExports{T, TMetadata}()"/> where
        /// <c>TMetadata</c> is an interface.
        /// </summary>
        /// <param name="catalog">The catalog from which constructed ExportProviders may have this support added.</param>
        /// <returns>The catalog with the additional support.</returns>
        public static ComposableCatalog WithMetadataViewEmitProxySupport(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));

            return catalog.AddPart(ProxySupportPartDefinition);
        }

        [PartNotDiscoverable]
        [PartMetadata(CompositionConstants.DgmlCategoryPartMetadataName, new string[] { "VsMEFBuiltIn" })]
        [Export(typeof(IMetadataViewProvider))]
        [ExportMetadata("OrderPrecedence", 50)]
        private class MetadataViewProxyProvider : IMetadataViewProvider
        {
            public bool IsMetadataViewSupported(Type metadataType)
            {
                // We apply to interfaces with nothing but property getters.
                return metadataType.IsInterface
                    && metadataType.GetMembers().All(IsPropertyRelated);
            }

            public object CreateProxy(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultValues, Type metadataViewType)
            {
                var factory = MetadataViewGenerator.GetMetadataViewFactory(metadataViewType);
                return factory(metadata, defaultValues);
            }

            private static bool IsPropertyRelated(MemberInfo member)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    return property.GetMethod != null && property.SetMethod == null;
                }

                var method = member as MethodInfo;
                if (method != null)
                {
                    return method.IsSpecialName
                        && method.Name.StartsWith("get_");
                }

                return false;
            }
        }
    }
}
