// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides helper methods for concrete metadata view implementations that are referenced by
/// <see cref="System.ComponentModel.Composition.MetadataViewImplementationAttribute"/>.
/// </summary>
public abstract class MetadataView
{
    private IReadOnlyDictionary<string, object?>? defaultValues;
    private IReadOnlyDictionary<string, object?>? metadata;

    /// <summary>
    /// Gets a metadata value using the calling property's name as the metadata key.
    /// </summary>
    /// <typeparam name="T">The expected metadata value type.</typeparam>
    /// <param name="propertyName">The name of the property whose metadata should be read.</param>
    /// <returns>The metadata value.</returns>
    protected T GetMetadata<T>([CallerMemberName] string propertyName = "")
    {
        Requires.NotNullOrEmpty(propertyName, nameof(propertyName));
        Verify.Operation(this.metadata is object && this.defaultValues is object, "This metadata view has not been initialized.");

        if (!this.metadata.TryGetValue(propertyName, out object? value))
        {
            value = this.defaultValues[propertyName];
        }

        return (T)value!;
    }

    internal void Initialize(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues)
    {
        Requires.NotNull(metadata, nameof(metadata));
        Requires.NotNull(defaultValues, nameof(defaultValues));
        Verify.Operation(this.metadata is null && this.defaultValues is null, "This metadata view has already been initialized.");

        this.metadata = metadata;
        this.defaultValues = defaultValues;
    }

    internal static void ThrowIfDirectMetadataViewType(Type metadataViewType)
    {
        Requires.NotNull(metadataViewType, nameof(metadataViewType));

        if (IsDirectMetadataViewType(metadataViewType))
        {
            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.MetadataViewDirectUseUnsupported, metadataViewType.FullName));
        }
    }

    internal static bool IsDirectMetadataViewType(Type metadataViewType)
    {
        Requires.NotNull(metadataViewType, nameof(metadataViewType));
        return typeof(MetadataView).IsAssignableFrom(metadataViewType);
    }
}
