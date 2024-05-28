// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using MessagePack;

    internal static class MessagePackOptionsExtensions
    {
        public static bool TryPrepareDeserializeReusableObject<T>(this MessagePackSerializerOptions option, out uint id, out T? value, ref MessagePackReader reader)
                   where T : class
        {
            var messagePackFormatterContext = (MessagePackSerializerContext)option!;
            return messagePackFormatterContext.TryPrepareDeserializeReusableObject(out id, out value, ref reader, option);
        }

        public static void OnDeserializedReusableObject(this MessagePackSerializerOptions option, uint id, object? value)
        {
            var messagePackFormatterContext = (MessagePackSerializerContext)option!;
            messagePackFormatterContext.OnDeserializedReusableObject(id, value!);
        }

        public static bool TryPrepareSerializeReusableObject(this MessagePackSerializerOptions option, object? value, ref MessagePackWriter writer)
        {
            var messagePackFormatterContext = (MessagePackSerializerContext)option!;
            return messagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, option);
        }

        public static Resolver CompositionResolver(this MessagePackSerializerOptions option)
        {
            var messagePackFormatterContext = (MessagePackSerializerContext)option!;
            return messagePackFormatterContext.CompositionResolver;
        }
    }
}
