// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using MessagePack;

    internal static class MessagePackOptionsExtensions
    {
        public static Resolver CompositionResolver(this MessagePackSerializerOptions option)
        {
            var messagePackFormatterContext = (MessagePackSerializerContext)option!;
            return messagePackFormatterContext.CompositionResolver;
        }
    }
}
