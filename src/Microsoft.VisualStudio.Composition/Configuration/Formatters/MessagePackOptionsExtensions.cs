// Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT license. See
// LICENSE file in the project root for full license information. Copyright (c) Microsoft
// Corporation. All rights reserved. Licensed under the MIT license. See LICENSE file in the project
// root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using MessagePack;

    public static class MessagePackOptionsExtensions
    {
        public static bool TryPrepareDeserializeReusableObject<T>(this MessagePackSerializerOptions option, out uint id, out T? value, ref MessagePackReader reader, MessagePackSerializerOptions options)
            where T : class
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            return messagePackFormatterContext.TryPrepareDeserializeReusableObject(out id, out value, ref reader, options);
        }

        public static void OnDeserializedReusableObject(this MessagePackSerializerOptions option, uint id, object value)
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            messagePackFormatterContext.OnDeserializedReusableObject(id, value);
        }

        public static bool TryPrepareSerializeReusableObject(this MessagePackSerializerOptions option, object value, ref MessagePackWriter writer, MessagePackSerializerOptions options)
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            return messagePackFormatterContext.TryPrepareSerializeReusableObject(value, ref writer, options);
        }

        public static Resolver CompositionResolver(this MessagePackSerializerOptions option)
        {
            MessagePackFormatterContext messagePackFormatterContext = option as MessagePackFormatterContext;
            return messagePackFormatterContext.CompositionResolver;
        }
    }
}
