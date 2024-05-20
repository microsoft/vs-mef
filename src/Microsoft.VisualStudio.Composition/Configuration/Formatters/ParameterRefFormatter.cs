// Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT license. See
// LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ParameterRefFormatter : IMessagePackFormatter<ParameterRef>
    {
        /// <inheritdoc/>
        public ParameterRef Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out ParameterRef? value, ref reader))
            {
                MethodRef method = options.Resolver.GetFormatterWithVerify<MethodRef>().Deserialize(ref reader, options);
                int parameterIndex = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                value = new ParameterRef(method, parameterIndex);

                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ParameterRef value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                options.Resolver.GetFormatterWithVerify<MethodRef>().Serialize(ref writer, value.Method, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.ParameterIndex, options);
            }
        }
    }
}
