// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class PartCreationPolicyConstraintFormatter : BaseMessagePackFormatter<PartCreationPolicyConstraint>
    {
        public static readonly PartCreationPolicyConstraintFormatter Instance = new();

        private PartCreationPolicyConstraintFormatter()
            : base(expectedArrayElementCount: 1)
        {
        }

        protected override PartCreationPolicyConstraint DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            CreationPolicy creationPolicy = options.Resolver.GetFormatterWithVerify<CreationPolicy>().Deserialize(ref reader, options);
            return PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(creationPolicy)!;
        }

        protected override void SerializeData(ref MessagePackWriter writer, PartCreationPolicyConstraint value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<CreationPolicy>().Serialize(ref writer, value.RequiredCreationPolicy, options);
        }
    }
}
