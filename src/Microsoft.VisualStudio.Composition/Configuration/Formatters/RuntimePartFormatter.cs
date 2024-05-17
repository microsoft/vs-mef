// Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT license. See
// LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;
    using static Microsoft.VisualStudio.Composition.RuntimeComposition;

    internal class RuntimePartFormatter : IMessagePackFormatter<RuntimePart>
    {
        /// <inheritdoc/>
        public RuntimePart Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var importingCtor = default(MethodRef);
            IReadOnlyList<RuntimeComposition.RuntimeImport> importingCtorArguments = ImmutableList<RuntimeComposition.RuntimeImport>.Empty;

            TypeRef typeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            IReadOnlyList<RuntimeExport> exports = CollectionFormatter<RuntimeExport>.DeserializeCollection(ref reader, options);
            bool hasCtor = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);

            if (hasCtor)
            {
                importingCtor = options.Resolver.GetFormatterWithVerify<MethodRef?>().Deserialize(ref reader, options);
                importingCtorArguments = CollectionFormatter<RuntimeImport>.DeserializeCollection(ref reader, options);
            }

            IReadOnlyList<RuntimeImport> importingMembers = CollectionFormatter<RuntimeImport>.DeserializeCollection(ref reader, options);
            IReadOnlyList<MethodRef> onImportsSatisfiedMethods = CollectionFormatter<MethodRef>.DeserializeCollection(ref reader, options);

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
            CollectionFormatter<RuntimeExport>.SerializeCollection(ref writer, value.Exports, options);

            if (value.ImportingConstructorOrFactoryMethodRef is null)
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, false, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, true, options);
                options.Resolver.GetFormatterWithVerify<MethodRef?>().Serialize(ref writer, value.ImportingConstructorOrFactoryMethodRef, options);

                CollectionFormatter<RuntimeImport>.SerializeCollection(ref writer, value.ImportingConstructorArguments, options);
            }

            CollectionFormatter<RuntimeImport>.SerializeCollection(ref writer, value.ImportingMembers, options);
            CollectionFormatter<MethodRef>.SerializeCollection(ref writer, value.OnImportsSatisfiedMethodRefs, options);

            options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.SharingBoundary, options);
        }
    }
}
