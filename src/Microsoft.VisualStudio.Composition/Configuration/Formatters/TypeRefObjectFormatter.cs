// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Immutable;
    using MessagePack;
    using MessagePack.Formatters;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class TypeRefObjectFormatter : IMessagePackFormatter<TypeRef>
    {
        /// <inheritdoc/>
        public TypeRef Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            StrongAssemblyIdentity assemblyId = options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Deserialize(ref reader, options);
            int metadataToken = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
            string fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
            TypeRefFlags flags = options.Resolver.GetFormatterWithVerify<TypeRefFlags>().Deserialize(ref reader, options);
            int genericTypeParameterCount = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
            ImmutableArray<TypeRef?> genericTypeArguments = ReadTypeRefImmutableArray(ref reader, options); //todo
            bool shallow = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
            ImmutableArray<TypeRef?> baseTypes = !shallow ? ReadTypeRefImmutableArray(ref reader, options)
                : ImmutableArray<TypeRef?>.Empty; //todo

            bool hasElementType = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options) != 0;

            TypeRef? elementType = hasElementType
                   ? options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options)
                   : null;

            Resolver resolver = options.Resolver.GetFormatterWithVerify<Resolver>().Deserialize(ref reader, options);

            var value = TypeRef.Get(resolver, assemblyId, metadataToken, fullName, flags, genericTypeParameterCount, genericTypeArguments!, shallow, baseTypes!, elementType);


            return value;
        }

        public void Serialize(ref MessagePackWriter writer, TypeRef value, MessagePackSerializerOptions options)
        {
            options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Serialize(ref writer, value.AssemblyId, options);
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.FullName, options);
            options.Resolver.GetFormatterWithVerify<TypeRefFlags>().Serialize(ref writer, value.TypeFlags, options);
            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.GenericTypeParameterCount, options);            
            CollectionFormatter<TypeRef>.SerializeCollection(ref writer, value.GenericTypeArguments, options); //todo
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsShallow, options);

            if (!value.IsShallow)
            {
                //options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.BaseTypes, options); //tdo
                CollectionFormatter<TypeRef>.SerializeCollection(ref writer, value.BaseTypes, options); //todo
            }

            options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.ElementTypeRef.Equals(value) ? 0 : 1, options);

            if (!value.ElementTypeRef.Equals(value))
            {
                options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ElementTypeRef, options);
            }

            options.Resolver.GetFormatterWithVerify<Resolver>().Serialize(ref writer, value.Resolver, options);
        }

        internal static ImmutableArray<TypeRef?> ReadTypeRefImmutableArray(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
            switch (count)
            {
                case 0:
                    return ImmutableArray<TypeRef?>.Empty;
                case 1:
                    return ImmutableArray.Create(ReadTypeRef(ref reader, options));
                case 2:
                    return ImmutableArray.Create(ReadTypeRef(ref reader, options), ReadTypeRef(ref reader, options));
                case 3:
                    return ImmutableArray.Create(ReadTypeRef(ref reader, options), ReadTypeRef(ref reader, options), ReadTypeRef(ref reader, options));
                case 4:
                    return ImmutableArray.Create(ReadTypeRef(ref reader, options), ReadTypeRef(ref reader, options), ReadTypeRef(ref reader, options), ReadTypeRef(ref reader, options));
            }

            if (count > 0xffff)
            {
                // Probably either file corruption or a bug in serialization.
                // Let's not take untold amounts of memory by throwing out suspiciously large lengths.
                throw new NotSupportedException();
            }

            Stack<ImmutableArray<TypeRef?>.Builder> typeRefBuilders = new Stack<ImmutableArray<TypeRef?>.Builder>();

            // Larger arrays need to use a builder to prevent duplicate array allocations.
            // Reuse builders to save on GC pressure
            ImmutableArray<TypeRef?>.Builder builder = typeRefBuilders.Count > 0 ? typeRefBuilders.Pop() : ImmutableArray.CreateBuilder<TypeRef?>();

            builder.Capacity = (int)count;
            for (int i = 0; i < count; i++)
            {
                builder.Add(ReadTypeRef(ref reader, options));
            }

            ImmutableArray<TypeRef?> result = builder.MoveToImmutable();

            // Place builder back in cache
            typeRefBuilders.Push(builder);

            return result;

            TypeRef? ReadTypeRef(ref MessagePackReader messagePackReader, MessagePackSerializerOptions messagePackSerializerOptions)
            {
                return options.Resolver.GetFormatterWithVerify<TypeRef?>().Deserialize(ref messagePackReader, messagePackSerializerOptions);
            }
        }
    }
}
