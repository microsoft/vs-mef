// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Formatter
{
    using System.Collections.Immutable;
    using System.Reflection;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    internal class TypeRefObjectFormatter : IMessagePackFormatter<TypeRef?>
    {
        public static readonly TypeRefObjectFormatter Instance = new();

        private TypeRefObjectFormatter()
        {
        }

        /// <inheritdoc/>
        public TypeRef? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareDeserializeReusableObject(out uint id, out TypeRef? value, ref reader))
            {
                IMessagePackFormatter<ImmutableArray<TypeRef?>> typeRefFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef?>>();
                StrongAssemblyIdentity assemblyId = options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Deserialize(ref reader, options);
                int metadataToken = reader.ReadInt32();
                string fullName = reader.ReadString()!;
                TypeRefFlags flags = options.Resolver.GetFormatterWithVerify<TypeRefFlags>().Deserialize(ref reader, options);
                int genericTypeParameterCount = reader.ReadInt32();
                ImmutableArray<TypeRef?> genericTypeArguments = typeRefFormatter.Deserialize(ref reader, options);
                bool shallow = reader.ReadBoolean();
                ImmutableArray<TypeRef?> baseTypes = !shallow ? typeRefFormatter.Deserialize(ref reader, options) : ImmutableArray<TypeRef?>.Empty;
                bool hasElementType = reader.ReadInt32() != 0;
                TypeRef? elementType = hasElementType
                       ? options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options)
                       : null;

                value = TypeRef.Get(options.CompositionResolver(), assemblyId, metadataToken, fullName, flags, genericTypeParameterCount, genericTypeArguments!, shallow, baseTypes!, elementType);
                options.OnDeserializedReusableObject(id, value);
            }

            return value;
        }

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, TypeRef? value, MessagePackSerializerOptions options)
        {
            if (options.TryPrepareSerializeReusableObject(value, ref writer))
            {
                options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Serialize(ref writer, value!.AssemblyId, options);
                writer.Write(value.MetadataToken);
                writer.Write(value.FullName);
                options.Resolver.GetFormatterWithVerify<TypeRefFlags>().Serialize(ref writer, value.TypeFlags, options);
                writer.Write(value.GenericTypeParameterCount);
                options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.GenericTypeArguments, options);
                writer.Write(value.IsShallow);

                if (!value.IsShallow)
                {
                    options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.BaseTypes, options);
                }

                writer.Write(value.ElementTypeRef.Equals(value) ? 0 : 1);

                if (!value.ElementTypeRef.Equals(value))
                {
                    options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ElementTypeRef, options);
                }
            }
        }
    }
}
