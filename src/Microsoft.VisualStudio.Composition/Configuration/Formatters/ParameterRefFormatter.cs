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
            : base(expectedArrayElementCount: 2)
        {
        }

        /// <inheritdoc/>
        protected override ParameterRef? DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            MethodRef method = options.Resolver.GetFormatterWithVerify<MethodRef>().Deserialize(ref reader, options);
            int parameterIndex = reader.ReadInt32();
            return new ParameterRef(method, parameterIndex);
        }

        /// <inheritdoc/>
        protected override void SerializeData(ref MessagePackWriter writer, ParameterRef? value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<MethodRef>().Serialize(ref writer, value!.Method, options);
            writer.Write(value.ParameterIndex);
        }
    }
}
