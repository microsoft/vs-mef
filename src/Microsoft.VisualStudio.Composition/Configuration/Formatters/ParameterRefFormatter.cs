// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ParameterRefFormatter : BaseMessagePackFormatter<ParameterRef?>
    {
        public static readonly ParameterRefFormatter Instance = new();

        private ParameterRefFormatter()
        {
        }

        /// <inheritdoc/>
        protected override ParameterRef? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out ParameterRef? value, ref reader))
            {
                this.CheckArrayHeaderCount(ref reader, 2);
                MethodRef method = options.Resolver.GetFormatterWithVerify<MethodRef>().Deserialize(ref reader, options);
                int parameterIndex = reader.ReadInt32();
                value = new ParameterRef(method, parameterIndex);

                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, ParameterRef? value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                writer.WriteArrayHeader(2);
                options.Resolver.GetFormatterWithVerify<MethodRef>().Serialize(ref writer, value!.Method, options);
                writer.Write(value.ParameterIndex);
            }
        }
    }
}
