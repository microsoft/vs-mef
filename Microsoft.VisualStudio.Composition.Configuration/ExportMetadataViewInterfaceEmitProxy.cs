namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class ExportMetadataViewInterfaceEmitProxy
    {
        private static readonly ComposablePartDefinition proxySupportPartDefinition = new AttributedPartDiscovery().CreatePart(typeof(MetadataViewProxyProvider));

        /// <summary>
        /// Adds support for queries to <see cref="ExportProvider.GetExports{T, TMetadata}()"/> where
        /// <c>TMetadata</c> is an interface.
        /// </summary>
        /// <param name="catalog">The catalog from which constructed ExportProviders may have this support added.</param>
        /// <returns>The catalog with the additional support.</returns>
        public static ComposableCatalog WithMetadataViewEmitProxySupport(this ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            return catalog.WithPart(proxySupportPartDefinition);
        }

        [PartNotDiscoverable]
        [Export(typeof(IMetadataViewProvider))]
        [ExportMetadata("Order", 100)]
        private class MetadataViewProxyProvider : IMetadataViewProvider
        {
            public bool IsMetadataViewSupported(Type metadataType)
            {
                // We apply to interfaces with nothing but property getters.
                return metadataType.IsInterface
                    && metadataType.GetMembers().All(m => (m.MemberType == MemberTypes.Method && ((MethodInfo)m).Attributes.HasFlag(MethodAttributes.SpecialName) && m.Name.StartsWith("get_")) || (m.MemberType == System.Reflection.MemberTypes.Property && ((PropertyInfo)m).GetGetMethod() != null && ((PropertyInfo)m).GetSetMethod() == null));
            }

            public object CreateProxy(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultValues, Type metadataViewType)
            {
                var factory = MetadataViewGenerator.GetMetadataViewFactory(metadataViewType);
                return factory(metadata, defaultValues);
            }
        }
    }
}
