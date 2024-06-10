// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimePartFormatter : IMessagePackFormatter<RuntimePart?>
    {
        public static readonly RuntimePartFormatter Instance = new();

        private RuntimePartFormatter()
        {
        }

        /// <inheritdoc/>
        public RuntimePart? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            try
            {
                var actualCount = reader.ReadArrayHeader();
                if (actualCount != 8)
                {
                    throw new MessagePackSerializationException($"Invalid array count for type {nameof(RuntimePart)}. Expected: {8}, Actual: {actualCount}");
                }

                var importingCtor = default(MethodRef);
                IReadOnlyList<RuntimeComposition.RuntimeImport> importingCtorArguments = ImmutableList<RuntimeComposition.RuntimeImport>.Empty;

                TypeRef typeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

                IMessagePackFormatter<IReadOnlyList<RuntimeExport>> runtimeExportFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimeExport>>();
                IReadOnlyList<RuntimeExport> exports = runtimeExportFormatter.Deserialize(ref reader, options);

                IMessagePackFormatter<IReadOnlyList<RuntimeImport>> runtimeImportFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<RuntimeImport>>();

                if (reader.TryReadNil())
                {
                    reader.Skip();
                }
                else
                {
                    importingCtor = options.Resolver.GetFormatterWithVerify<MethodRef?>().Deserialize(ref reader, options);
                    importingCtorArguments = runtimeImportFormatter.Deserialize(ref reader, options);
                }

                IReadOnlyList<RuntimeImport> importingMembers = runtimeImportFormatter.Deserialize(ref reader, options);
                IReadOnlyList<MethodRef> onImportsSatisfiedMethods = options.Resolver.GetFormatterWithVerify<IReadOnlyList<MethodRef>>().Deserialize(ref reader, options);

                string? sharingBoundary = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);

                return new RuntimeComposition.RuntimePart(
                          typeRef,
                          importingCtor,
                          importingCtorArguments,
                          importingMembers,
                          exports,
                          onImportsSatisfiedMethods,
                          sharingBoundary);
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, RuntimePart? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(8);

            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.TypeRef, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeExport>>().Serialize(ref writer, value.Exports, options);

            IMessagePackFormatter<IReadOnlyCollection<RuntimeImport>> runtimeImportFormatter = options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<RuntimeImport>>();

            if (value.ImportingConstructorOrFactoryMethodRef is null)
            {
                writer.WriteNil(); // no Importing Constructor Or Factory MethodRef arguments
                writer.WriteNil(); // no importing constructor arguments
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<MethodRef?>().Serialize(ref writer, value.ImportingConstructorOrFactoryMethodRef, options);
                runtimeImportFormatter.Serialize(ref writer, value.ImportingConstructorArguments, options);
            }

            runtimeImportFormatter.Serialize(ref writer, value.ImportingMembers, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<MethodRef>>().Serialize(ref writer, value.OnImportsSatisfiedMethodRefs, options);

            options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.SharingBoundary, options);
        }
    }
}
