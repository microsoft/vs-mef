// Copyright (c) Microsoft. All rights reserved.

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
    /// Supports metadata views that are concrete classes with a public default constructor
    /// and properties with set accessors.
    /// </summary>
    internal class MetadataViewClassDefaultCtorProvider : IMetadataViewProvider
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewClassDefaultCtorProvider), 1100000, Resolver.DefaultInstance);

        internal static readonly IMetadataViewProvider Default = new MetadataViewClassDefaultCtorProvider();

        private MetadataViewClassDefaultCtorProvider()
        {
        }

        public bool IsMetadataViewSupported(Type metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));
            var typeInfo = metadataType.GetTypeInfo();

            return typeInfo.IsClass && !typeInfo.IsAbstract && FindConstructor(typeInfo) != null;
        }

        public object CreateProxy(IReadOnlyDictionary<string, object> metadata, IReadOnlyDictionary<string, object> defaultValues, Type metadataViewType)
        {
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(defaultValues, nameof(defaultValues));
            Requires.NotNull(metadataViewType, nameof(metadataViewType));

            TypeInfo typeInfo = metadataViewType.GetTypeInfo();
            var view = FindConstructor(typeInfo).Invoke(Type.EmptyTypes);

            foreach (var propertyInfo in metadataViewType.EnumProperties().WherePublicInstance())
            {
                if ((metadata.TryGetValue(propertyInfo.Name, out object value) || defaultValues.TryGetValue(propertyInfo.Name, out value)) && propertyInfo.SetMethod != null)
                {
                    propertyInfo.SetValue(view, value);
                }
            }

            return view;
        }

        private static ConstructorInfo FindConstructor(TypeInfo metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));

            return metadataType.DeclaredConstructors.FirstOrDefault(ctor => ctor.GetParameters().Length == 0 && ctor.IsPublic);
        }
    }
}
