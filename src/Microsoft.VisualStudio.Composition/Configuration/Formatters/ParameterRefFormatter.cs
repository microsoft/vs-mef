// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ParameterRefFormatter : IMessagePackFormatter<ParameterRef>
    {
        /// <inheritdoc/>
        public ParameterRef Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            MethodRef method = options.Resolver.GetFormatterWithVerify<MethodRef>().Deserialize(ref reader, options);
            int parameterIndex = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
            var value = new ParameterRef(method, parameterIndex);

            return value;
        }

        public void Serialize(ref MessagePackWriter writer, ParameterRef value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<MethodRef>().Serialize(ref writer, value.Method, options);
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.ParameterIndex, options);
        }
    }
}
