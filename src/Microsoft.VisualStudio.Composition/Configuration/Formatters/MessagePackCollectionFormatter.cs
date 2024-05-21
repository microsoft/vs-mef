﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Reflection.PortableExecutable;
    using MessagePack;
    using MessagePack.Formatters;

#pragma warning disable CS3001 // Argument type is not CLS-compliant
#pragma warning disable SA1649 // File name should match first type name

    public class MessagePackCollectionFormatter<T> : IMessagePackFormatter<IReadOnlyCollection<T>>
    {
        /// <inheritdoc/>
        public IReadOnlyCollection<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return DeserializeCollection(ref reader, options);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, IReadOnlyCollection<T> value, MessagePackSerializerOptions options)
        {
            SerializeCollection(ref writer, value, options);
        }

        internal static void SerializeCollection(ref MessagePackWriter writer, IReadOnlyCollection<T> value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.Count(), options);
            foreach (T item in value)
            {
                options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, item, options);
            }
        }

        internal static IReadOnlyList<T> DeserializeCollection(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

            if (count == 0)
            {
                return Array.Empty<T>();
            }

            var collection = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                collection.Add(options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options));
            }

            return collection;
        }
    }
}