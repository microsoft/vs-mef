// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides helper methods for concrete metadata view implementations that are referenced by
/// <see cref="System.ComponentModel.Composition.MetadataViewImplementationAttribute"/>.
/// </summary>
public abstract class MetadataView
{
    private static readonly object DefaultValuesByTypeCacheLock = new();
    private static readonly Dictionary<Type, IReadOnlyDictionary<string, object?>> DefaultValuesByTypeCache = new();
    private IReadOnlyDictionary<string, object?>? defaultValues;
    private IReadOnlyDictionary<string, object?>? metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataView"/> class.
    /// </summary>
    protected MetadataView()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataView"/> class from a metadata dictionary,
    /// for compatibility with MEFv1 metadata view activation.
    /// </summary>
    /// <param name="metadata">The metadata dictionary.</param>
    protected MetadataView(IDictionary<string, object> metadata)
    {
        Requires.NotNull(metadata, nameof(metadata));
        this.Initialize(new DictionaryAdapter(metadata), GetDefaultValues(this.GetType()));
    }

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

        if (value is T typedValue)
        {
            return typedValue;
        }

        if (default(T) is null)
        {
            return default!;
        }

        string actualTypeName = value?.GetType().FullName ?? "null";
        throw new InvalidCastException($"Metadata value '{propertyName}' could not be cast from '{actualTypeName}' to '{typeof(T).FullName}'.");
    }

    internal void Initialize(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues)
    {
        Requires.NotNull(metadata, nameof(metadata));
        Requires.NotNull(defaultValues, nameof(defaultValues));
        Verify.Operation(this.metadata is null && this.defaultValues is null, "This metadata view has already been initialized.");

        this.metadata = metadata;
        this.defaultValues = defaultValues;
    }

    internal static bool IsDirectMetadataViewType(Type metadataViewType)
    {
        Requires.NotNull(metadataViewType, nameof(metadataViewType));
        return typeof(MetadataView).IsAssignableFrom(metadataViewType);
    }

    private static IReadOnlyDictionary<string, object?> GetDefaultValues(Type metadataViewType)
    {
        lock (DefaultValuesByTypeCacheLock)
        {
            if (DefaultValuesByTypeCache.TryGetValue(metadataViewType, out IReadOnlyDictionary<string, object?>? cached))
            {
                return cached;
            }
        }

        var builder = ImmutableDictionary.CreateBuilder<string, object?>();
        foreach (PropertyInfo property in metadataViewType.EnumProperties().WherePublicInstance())
        {
            DefaultValueAttribute? defaultValueAttribute = property.GetFirstAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute is not null && !builder.ContainsKey(property.Name))
            {
                builder.Add(property.Name, defaultValueAttribute.Value);
            }
        }

        foreach (Type interfaceType in metadataViewType.GetTypeInfo().ImplementedInterfaces)
        {
            foreach (PropertyInfo property in interfaceType.GetTypeInfo().DeclaredProperties)
            {
                DefaultValueAttribute? defaultValueAttribute = property.GetFirstAttribute<DefaultValueAttribute>();
                if (defaultValueAttribute is not null && !builder.ContainsKey(property.Name))
                {
                    builder.Add(property.Name, defaultValueAttribute.Value);
                }
            }
        }

        IReadOnlyDictionary<string, object?> result = builder.ToImmutable();
        lock (DefaultValuesByTypeCacheLock)
        {
            DefaultValuesByTypeCache[metadataViewType] = result;
        }

        return result;
    }

    /// <summary>
    /// Exposes an <see cref="IDictionary{TKey, TValue}"/> as an <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    private sealed class DictionaryAdapter(IDictionary<string, object> metadata) : IReadOnlyDictionary<string, object?>
    {
        public IEnumerable<string> Keys => metadata.Keys;

        public IEnumerable<object?> Values => metadata.Values;

        public int Count => metadata.Count;

        public object? this[string key] => metadata[key];

        public bool ContainsKey(string key) => metadata.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (KeyValuePair<string, object> item in metadata)
            {
                yield return new KeyValuePair<string, object?>(item.Key, item.Value);
            }
        }

        public bool TryGetValue(string key, out object? value)
        {
            bool result = metadata.TryGetValue(key, out object? actualValue);
            value = actualValue;
            return result;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
