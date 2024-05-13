// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Reflection.PortableExecutable;
    using MessagePack;
    using MessagePack.Formatters;

    public class CollectionFormatter<T> : IMessagePackFormatter<IReadOnlyCollection<T>>
        //where T : class
    {
        /// <inheritdoc/>
        public IReadOnlyCollection<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return DeserializeCollection(ref reader, options);
        }

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

        internal static IReadOnlyCollection<T> DeserializeCollection(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

            if (count == 0)
            {
                return Array.Empty<T>();
            }

            var list = new T[count];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
            }

            return list;
        }
    }
}
