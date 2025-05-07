// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Supports metadata views that are concrete classes with a public constructor
    /// that accepts the metadata dictionary as its only parameter.
    /// </summary>
    internal class MetadataViewClassProvider : IMetadataViewProvider
    {
        internal static readonly ComposablePartDefinition PartDefinition =
            Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewClassProvider), 1000000, Resolver.DefaultInstance);

        internal static readonly IMetadataViewProvider Default = new MetadataViewClassProvider();

        private MetadataViewClassProvider()
        {
        }

        public bool IsMetadataViewSupported(Type metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));
            var typeInfo = metadataType.GetTypeInfo();

            return typeInfo.IsClass && !typeInfo.IsAbstract && FindConstructor(typeInfo) != null;
        }

        public object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
        {
            ConstructorInfo? ctor = FindConstructor(metadataViewType.GetTypeInfo());
            Requires.Argument(ctor is not null, nameof(metadataViewType), "No public constructor with the required signature found.");

            // Avoid creating a new ImmutableDictionary if the passed in metadata is already of that form
            var metadataAsImmutableDictionary = metadata as ImmutableDictionary<string, object?> ?? ImmutableDictionary.CreateRange(metadata);
            return ctor.Invoke(new object[] { metadataAsImmutableDictionary });
        }

        private static ConstructorInfo? FindConstructor(TypeInfo metadataType)
        {
            Requires.NotNull(metadataType, nameof(metadataType));

            var publicCtorsWithOneParameter = from ctor in metadataType.DeclaredConstructors
                                              where ctor.IsPublic
                                              let parameters = ctor.GetParameters()
                                              where parameters.Length == 1
                                              let paramInfo = parameters[0].ParameterType.GetTypeInfo()
                                              where paramInfo.IsAssignableFrom(typeof(ImmutableDictionary<string, object?>).GetTypeInfo())
                                              select ctor;
            return publicCtorsWithOneParameter.FirstOrDefault();
        }
    }
}
