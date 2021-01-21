// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal static class Utilities
    {
        private const string CompositionErrorHeaderFormat = "----- CompositionError level {0} ------";

        internal static void WriteErrors(TextWriter textWriter, IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> errorStack)
        {
            int level = 1;
            foreach (IReadOnlyCollection<ComposedPartDiagnostic> errors in errorStack)
            {
                textWriter.WriteLine(string.Format(CultureInfo.CurrentCulture, CompositionErrorHeaderFormat, level++));
                foreach (ComposedPartDiagnostic error in errors)
                {
                    textWriter.WriteLine(error.Message);
                    foreach (ComposedPart part in error.Parts)
                    {
                        textWriter.WriteLine(string.Format(CultureInfo.CurrentCulture, "   part definition {0}", part.Definition.Type));
                    }

                    textWriter.WriteLine();
                }
            }
        }

        internal static ComposablePartDefinition GetMetadataViewProviderPartDefinition(Type providerType, int orderPrecedence, Resolver resolver)
        {
            Requires.NotNull(providerType, nameof(providerType));
            Requires.NotNull(resolver, nameof(resolver));

            var exportDefinition = new ExportDefinition(
                ContractNameServices.GetTypeIdentity(typeof(IMetadataViewProvider)),
                PartCreationPolicyConstraint.GetExportMetadata(CreationPolicy.Shared)
                    .AddRange(ExportTypeIdentityConstraint.GetExportMetadata(typeof(IMetadataViewProvider)))
                    .SetItem("OrderPrecedence", orderPrecedence));

            var partDefinition = new ComposablePartDefinition(
                TypeRef.Get(providerType, resolver),
                ImmutableDictionary<string, object?>.Empty.Add(CompositionConstants.DgmlCategoryPartMetadataName, new[] { "VsMEFBuiltIn" }),
                new[] { exportDefinition },
                ImmutableDictionary<MemberRef, IReadOnlyCollection<ExportDefinition>>.Empty,
                ImmutableList<ImportDefinitionBinding>.Empty,
                string.Empty,
                default(MethodRef),
                MethodRef.Get(providerType.GetTypeInfo().GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single(c => c.GetParameters().Length == 0), resolver),
                ImmutableList<ImportDefinitionBinding>.Empty,
                CreationPolicy.Shared,
                false);

            return partDefinition;
        }

        internal static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue?> dictionary, TKey key)
            where TKey : notnull
            where TValue : class
        {
            dictionary.TryGetValue(key, out TValue? value);
            return value;
        }

        [return: NotNullIfNotNull("defaultValue")]
        internal static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue? defaultValue)
            where TKey : notnull
            where TValue : class
        {
            if (!dictionary.TryGetValue(key, out TValue? value))
            {
                value = defaultValue;
            }

            return value;
        }

        internal static bool EqualsByValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> expected, IReadOnlyDictionary<TKey, TValue> actual, IEqualityComparer<TValue>? valueComparer = null)
            where TKey : notnull
        {
            Requires.NotNull(expected, nameof(expected));
            Requires.NotNull(actual, nameof(actual));
            valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;

            if (expected.Count != actual.Count)
            {
                return false;
            }

            foreach (var entry in expected)
            {
                TValue actualValue;
                if (!actual.TryGetValue(entry.Key, out actualValue))
                {
                    // missing key
                    return false;
                }

                if (!valueComparer.Equals(entry.Value, actualValue))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool TryGetValue<TValue>(this IReadOnlyDictionary<string, object?> metadata, string key, [MaybeNull, NotNullWhen(true)] out TValue value)
        {
            object? valueObject;
            if (metadata.TryGetValue(key, out valueObject) && valueObject is TValue)
            {
                value = (TValue)valueObject;
                return true;
            }

            value = default(TValue);
            return false;
        }

        internal static string MakeIdentifierNameSafe(string value)
        {
            return value
                .Replace('`', '_')
                .Replace('.', '_')
                .Replace('+', '_')
                .Replace('{', '_')
                .Replace('}', '_')
                .Replace('(', '_')
                .Replace(')', '_')
                .Replace(',', '_')
                .Replace('-', '_');
        }

        internal static bool Contains<T>(this ImmutableStack<T> stack, T value)
        {
            Requires.NotNull(stack, nameof(stack));

            while (!stack.IsEmpty)
            {
                if (EqualityComparer<T>.Default.Equals(value, stack.Peek()))
                {
                    return true;
                }

                stack = stack.Pop();
            }

            return false;
        }

        internal static bool EqualsByValue<T>(this ImmutableArray<T> array, ImmutableArray<T> other)
            where T : IEquatable<T>
        {
            if (array.Length != other.Length)
            {
                return false;
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Equals(other[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ToString(this IReadOnlyDictionary<string, object?> metadata, IndentingTextWriter writer)
        {
            Requires.NotNull(metadata, nameof(metadata));
            Requires.NotNull(writer, nameof(writer));

            foreach (var item in metadata)
            {
                writer.WriteLine("{0} = {1}", item.Key, item.Value);
            }
        }

        internal static void ToString(this object value, TextWriter writer)
        {
            Requires.NotNull(value, nameof(value));
            Requires.NotNull(writer, nameof(writer));

            var descriptiveValue = value as IDescriptiveToString;
            if (descriptiveValue != null)
            {
                descriptiveValue.ToString(writer);
            }
            else
            {
                writer.WriteLine(value);
            }
        }

        internal static object SpecifyIfNull(this object? value)
        {
            return value == null ? "<null>" : value;
        }

        internal static void ReportNullSafe<T>(this IProgress<T>? progress, T value)
        {
            if (progress != null)
            {
                progress.Report(value);
            }
        }

        internal static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<TValue> source, Func<TValue, TKey> keySelector, int capacity)
            where TKey : notnull
        {
            var dictionary = new Dictionary<TKey, TValue>(capacity);
            foreach (var item in source)
            {
                dictionary.Add(keySelector(item), item);
            }

            return dictionary;
        }
    }
}
