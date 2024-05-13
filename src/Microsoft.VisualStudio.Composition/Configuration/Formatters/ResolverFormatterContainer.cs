// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;

    public class ResolverFormatter : IMessagePackFormatter<Resolver>
    {
        /// <inheritdoc/>
        public Resolver Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return ResolverFormatterContainer.Resolver;
        }

        public void Serialize(ref MessagePackWriter writer, Resolver value, MessagePackSerializerOptions options)
        {
            ResolverFormatterContainer.Resolver ??= value;
        }
    }

    internal class ResolverFormatterContainer
    {
        public static Resolver Resolver { get; set; }
    }
}
