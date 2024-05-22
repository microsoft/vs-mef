// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Reflection.PortableExecutable;
    using MessagePack;
    using MessagePack.Formatters;

#pragma warning disable CS3001 // Argument type is not CLS-compliant

    internal class MessagePackCollectionFormatter<TCollectionType> : IMessagePackFormatter<IReadOnlyCollection<TCollectionType>>
    {
        public static readonly MessagePackCollectionFormatter<TCollectionType> Instance = new();

        private MessagePackCollectionFormatter()
        {
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<TCollectionType> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return DeserializeCollection(ref reader, options);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, IReadOnlyCollection<TCollectionType> value, MessagePackSerializerOptions options)
        {
            SerializeCollection(ref writer, value, options);
        }

        internal static IReadOnlyList<TCollectionType> DeserializeCollection(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

            if (count == 0)
            {
                return Array.Empty<TCollectionType>();
            }

            IMessagePackFormatter<TCollectionType> tCollectionTypeFormatter = options.Resolver.GetFormatterWithVerify<TCollectionType>();

            var collection = new TCollectionType[count];
            for (int i = 0; i < count; i++)
            {
                collection[i] = tCollectionTypeFormatter.Deserialize(ref reader, options);
            }

            return collection;
        }

        internal static void SerializeCollection(ref MessagePackWriter writer, IReadOnlyCollection<TCollectionType> value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.Count(), options);
            foreach (TCollectionType item in value)
            {
                options.Resolver.GetFormatterWithVerify<TCollectionType>().Serialize(ref writer, item, options);
            }
        }
    }
}
