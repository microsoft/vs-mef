// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;

    internal class ExceptionFormatter<TExceptionType> : IMessagePackFormatter<TExceptionType>
        where TExceptionType : Exception
    {
        /// <inheritdoc/>
        public TExceptionType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public void Serialize(ref MessagePackWriter writer, TExceptionType value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Message, options);
        }
    }
}
