// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System.Reflection;
using Microsoft.VisualStudio.Composition.Reflection;

/// <summary>
/// Supports metadata views that derive from <see cref="MetadataView"/> and are used directly as TMetadata.
/// </summary>
internal class MetadataViewDirectProvider : IMetadataViewProvider
{
    internal static readonly ComposablePartDefinition PartDefinition =
        Utilities.GetMetadataViewProviderPartDefinition(typeof(MetadataViewDirectProvider), 1000500, Resolver.DefaultInstance);

    internal static readonly IMetadataViewProvider Default = new MetadataViewDirectProvider();

    private MetadataViewDirectProvider()
    {
    }

    public bool IsMetadataViewSupported(Type metadataType)
    {
        Requires.NotNull(metadataType, nameof(metadataType));
        var typeInfo = metadataType.GetTypeInfo();

        return typeInfo.IsClass && !typeInfo.IsAbstract && MetadataView.IsDirectMetadataViewType(metadataType) && FindConstructor(typeInfo) != null;
    }

    public object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType)
    {
        Requires.NotNull(metadata, nameof(metadata));
        Requires.NotNull(defaultValues, nameof(defaultValues));
        Requires.NotNull(metadataViewType, nameof(metadataViewType));

        ConstructorInfo? ctor = FindConstructor(metadataViewType.GetTypeInfo());
        Requires.Argument(ctor is not null, nameof(metadataViewType), "No public default constructor found.");
        var metadataView = (MetadataView)ctor.Invoke(Type.EmptyTypes);
        metadataView.Initialize(metadata, defaultValues);
        return metadataView;
    }

    private static ConstructorInfo? FindConstructor(TypeInfo metadataType)
    {
        Requires.NotNull(metadataType, nameof(metadataType));

        return metadataType.DeclaredConstructors.FirstOrDefault(ctor => ctor.GetParameters().Length == 0 && ctor.IsPublic);
    }
}
