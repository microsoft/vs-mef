// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Globalization;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class ExportTypeIdentityConstraintFormatter : BaseMessagePackFormatter<ExportTypeIdentityConstraint>
    {
        public static readonly ExportTypeIdentityConstraintFormatter Instance = new();

        private ExportTypeIdentityConstraintFormatter()
            : base(expectedArrayElementCount: 1)
        {
        }

        protected override ExportTypeIdentityConstraint DeserializeData(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string typeIdentityName = reader.ReadString()!;
            return new ExportTypeIdentityConstraint(typeIdentityName);
        }

        protected override void SerializeData(ref MessagePackWriter writer, ExportTypeIdentityConstraint value, MessagePackSerializerOptions options)
        {
            writer.Write(value.TypeIdentityName);
        }
    }
}
