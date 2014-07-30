namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides metadata view proxy instances for arbitrary metadata view interfaces.
    /// </summary>
    internal interface IMetadataViewProvider
    {
        /// <summary>
        /// Gets a value indicating whether the created metadata proxy requires
        /// default values to be included in the metadata supplied to
        /// <see cref="CreateProxy"/>.
        /// </summary>
        bool IsDefaultMetadataRequired { get; }

        /// <summary>
        /// Gets a value indicating whether this provider can create a metadata proxy for a given type.
        /// </summary>
        /// <param name="metadataType">The type of the required proxy.</param>
        /// <returns><c>true</c> if the provider can create a proxy for this type. Otherwise false.</returns>
        bool IsMetadataViewSupported(Type metadataType);

        /// <summary>
        /// Creates a metadata view that acts as a strongly-typed accessor
        /// to a metadata dictionary.
        /// </summary>
        /// <param name="metadata">The metadata dictionary.</param>
        /// <param name="metadataViewType">The type of metadata view to create.</param>
        /// <returns>The proxy instance.</returns>
        object CreateProxy(IReadOnlyDictionary<string, object> metadata, Type metadataViewType);
    }
}
