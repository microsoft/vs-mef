// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;

    /// <summary>
    /// Provides a base implementation for MessagePack formatters.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    internal abstract class BaseMessagePackFormatter<TRequest> : IMessagePackFormatter<TRequest?>
           where TRequest : class?
    {
        /// <inheritdoc/>
        public TRequest? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            TRequest? response;
            try
            {
                response = this.DeserializeData(ref reader, options);
            }
            finally
            {
                reader.Depth--;
            }

            return response;
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, TRequest? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            this.SerializeData(ref writer, value, options);
        }

        protected virtual void CheckArrayHeaderCount(ref MessagePackReader reader, int expectedCount)
        {
            var actualCount = reader.ReadArrayHeader();
            if (actualCount != expectedCount)
            {
                throw new MessagePackSerializationException($"Invalid array count for type {typeof(TRequest).Name}. Expected: {expectedCount}, Actual: {actualCount}");
            }
        }

        /// <summary>
        /// Deserializes the request from the MessagePackReader.
        /// </summary>
        /// <param name="reader">The MessagePackReader to read from.</param>
        /// <param name="options">The serializer options.</param>
        /// <returns>The deserialized request.</returns>
        protected abstract TRequest? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options);

        /// <summary>
        /// Serializes the request to the MessagePackWriter.
        /// </summary>
        /// <param name="writer">The MessagePackWriter to write to.</param>
        /// <param name="value">The value to serialize.</param>
        /// <param name="options">The serializer options.</param>
        protected abstract void SerializeData(ref MessagePackWriter writer, TRequest value, MessagePackSerializerOptions options);
    }
}
