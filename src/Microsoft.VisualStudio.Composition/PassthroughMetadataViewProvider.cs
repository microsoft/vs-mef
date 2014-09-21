namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    /// <summary>
    /// Supports metadata views that are any type that <see cref="ImmutableDictionary{TKey, TValue}"/>
    /// could be assigned to, including <see cref="IDictionary{TKey, TValue}"/> and <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    internal class PassthroughMetadataViewProvider : IMetadataViewProvider
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(PassthroughMetadataViewProvider), 1001000);

        private PassthroughMetadataViewProvider() { }

        internal static readonly IMetadataViewProvider Default = new PassthroughMetadataViewProvider();

        public bool IsMetadataViewSupported(Type metadataType)
        {
            Requires.NotNull(metadataType, "metadataType");

            return metadataType.GetTypeInfo().IsAssignableFrom(typeof(IReadOnlyDictionary<string, object>).GetTypeInfo())
                || metadataType.GetTypeInfo().IsAssignableFrom(typeof(IDictionary<string, object>).GetTypeInfo());
        }

        public object CreateProxy(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultValues, Type metadataViewType)
        {
            Requires.NotNull(metadata, "metadata");

            // This cast should work because our IsMetadataViewSupported method filters to those that do.
            return metadata;
        }
    }
}
