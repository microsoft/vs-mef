// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimePartFormatter : IMessagePackFormatter<RuntimePart>
    {
        public static readonly RuntimePartFormatter Instance = new();

        private RuntimePartFormatter()
        {
        }

        /// <inheritdoc/>
        public RuntimePart Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var importingCtor = default(MethodRef);
            IReadOnlyList<RuntimeComposition.RuntimeImport> importingCtorArguments = ImmutableList<RuntimeComposition.RuntimeImport>.Empty;

            TypeRef typeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            IReadOnlyList<RuntimeExport> exports = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimeExport>>().Deserialize(ref reader, options);

            bool hasCtor = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

            if (hasCtor)
            {
                importingCtor = options.Resolver.GetFormatterWithVerify<MethodRef?>().Deserialize(ref reader, options);
                importingCtorArguments = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimeImport>>().Deserialize(ref reader, options);
            }

            IReadOnlyList<RuntimeImport> importingMembers = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimeImport>>().Deserialize(ref reader, options);
            IReadOnlyList<MethodRef> onImportsSatisfiedMethods = options.Resolver.GetFormatterWithVerify<IReadOnlyList<MethodRef>>().Deserialize(ref reader, options);

            string sharingBoundary = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

            return new RuntimeComposition.RuntimePart(
                      typeRef,
                      importingCtor,
                      importingCtorArguments,
                      importingMembers,
                      exports!,
                      onImportsSatisfiedMethods,
                      sharingBoundary);
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimePart value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.TypeRef, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeExport>>().Serialize(ref writer, value.Exports, options);

            if (value.ImportingConstructorOrFactoryMethodRef is null)
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, false, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, true, options);
                options.Resolver.GetFormatterWithVerify<MethodRef?>().Serialize(ref writer, value.ImportingConstructorOrFactoryMethodRef, options);

                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeImport>>().Serialize(ref writer, value.ImportingConstructorArguments, options);
            }

            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeImport>>().Serialize(ref writer, value.ImportingMembers, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<MethodRef>>().Serialize(ref writer, value.OnImportsSatisfiedMethodRefs, options);

            options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.SharingBoundary, options);
        }
    }
}
