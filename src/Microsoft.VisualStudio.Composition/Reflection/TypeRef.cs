// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.



namespace Microsoft.VisualStudio.Composition
{
    using MessagePack.Formatters;
    using MessagePack;
    using System.Reflection;

    //public class MemberInfoFormatter : IMessagePackFormatter<MemberInfo>
    //{
    //    public void Serialize(ref MessagePackWriter writer, MemberInfo value, MessagePackSerializerOptions options)
    //    {
    //        options.Resolver.GetFormatterWithVerify<System.Reflection.MemberTypes>().Serialize(ref writer, value.MemberType, options);
            

    //        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.DeclaringType.FullName, options);
    //        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
    //    }

    //    public MemberInfo Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    //    {

    //        var memberInfoParts = options.Resolver.GetFormatterWithVerify<System.Reflection.MemberTypes>().Deserialize(ref reader, options);
    //        Type declaringType = Type.GetType(memberInfoParts);

    //        var fullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
    //        var name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);


    //        var obj = new MemberInfo();

    //        var Mvid = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);

    //        var FullName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);

    //        AssemblyName assemblyName = new AssemblyName(FullName);
    //        //assemblyName.Name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
    //        assemblyName.Version = new Version(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
    //        assemblyName.CultureName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
    //        assemblyName.ProcessorArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
    //        assemblyName.Flags = (AssemblyNameFlags)Enum.Parse(typeof(AssemblyNameFlags), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));

    //        assemblyName.CodeBase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);





    //        return new StrongAssemblyIdentity(assemblyName, Mvid);
    //    }
    //}
}

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using MessagePack;
    using System.Reflection;
    using MessagePack.Formatters;
    using System.Reflection.Metadata.Ecma335;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    //[MessagePackObject(true)]
    [MessagePackFormatter(typeof(TypeRefObjectFormatter))]
    public class TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
        class TypeRefObjectFormatter : IMessagePackFormatter<TypeRef>
        {
            public void Serialize(ref MessagePackWriter writer, TypeRef value, MessagePackSerializerOptions options)
            {
                // options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ArrayElementType, options);

                options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Serialize(ref writer, value.AssemblyId, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.MetadataToken, options);

                options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.FullName, options);
                options.Resolver.GetFormatterWithVerify<TypeRefFlags>().Serialize(ref writer, value.TypeFlags, options);
                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.GenericTypeParameterCount, options);
                options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.GenericTypeArguments, options);
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsShallow, options);
                if (!value.IsShallow)
                {
                    options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Serialize(ref writer, value.BaseTypes, options);
                }

                options.Resolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.ElementTypeRef.Equals(value) ? 0 : 1, options);

                if (!value.ElementTypeRef.Equals(value))
                {
                    options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ElementTypeRef, options);
                }


            }

            public TypeRef Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {


                var assemblyId = options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Deserialize(ref reader, options);
                int MetadataTokenInt = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                var MetadataTokenValue = MetadataTokenInt | (uint)MetadataTokenType.Type;
                var fullname = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                var typeFlags = options.Resolver.GetFormatterWithVerify<TypeRefFlags>().Deserialize(ref reader, options);
                var genericTypeParameterCount = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);

                var genericTypeArguments = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Deserialize(ref reader, options);

                var shallow = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                var baseTypes = shallow ? ImmutableArray<TypeRef>.Empty : options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Deserialize(ref reader, options);
                var elementType = options.Resolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options) == 0 ? null : options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

                TypeRef ool = null;

                return ool;

                // var value = TypeRef.Get(options.Resolver, assemblyId, MetadataTokenValue, fullname, typeFlags, genericTypeParameterCount, genericTypeArguments, shallow, baseTypes, elementType);


                // var ArrayElementType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);

                // var AssemblyId = options.Resolver.GetFormatterWithVerify<StrongAssemblyIdentity>().Deserialize(ref reader, options);

                // AssemblyName assemblyName = new AssemblyName();
                // assemblyName.Name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                // assemblyName.Version = new Version(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
                // assemblyName.CultureName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                // assemblyName.ProcessorArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
                // assemblyName.Flags = (AssemblyNameFlags)Enum.Parse(typeof(AssemblyNameFlags), options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));

                // var AssemblyName = assemblyName;

                // var Resolver = options.Resolver.GetFormatterWithVerify<Resolver>().Deserialize(ref reader, options);

                // var BaseTypes = options.Resolver.GetFormatterWithVerify<ImmutableArray<TypeRef>>().Deserialize(ref reader, options);




                //var value = TypeRef.Get(this.Resolver, assemblyId, metadataToken, fullName, flags, genericTypeParameterCount, genericTypeArguments!, shallow, baseTypes!, elementType);
                // return value;


                //return null;

                //    ImmutableArray<TypeRef?> ReadTypeRefImmutableArray(BinaryReader reader, Func<TypeRef?> itemReader)
                //    {
                //        uint count = this.ReadCompressedUInt();

                //        switch (count)
                //        {
                //            case 0:
                //                return ImmutableArray<TypeRef?>.Empty;
                //            case 1:
                //                return ImmutableArray.Create(itemReader());
                //            case 2:
                //                return ImmutableArray.Create(itemReader(), itemReader());
                //            case 3:
                //                return ImmutableArray.Create(itemReader(), itemReader(), itemReader());
                //            case 4:
                //                return ImmutableArray.Create(itemReader(), itemReader(), itemReader(), itemReader());
                //        }

                //        if (count > 0xffff)
                //        {
                //            // Probably either file corruption or a bug in serialization.
                //            // Let's not take untold amounts of memory by throwing out suspiciously large lengths.
                //            throw new NotSupportedException();
                //        }

                //        // Larger arrays need to use a builder to prevent duplicate array allocations.
                //        // Reuse builders to save on GC pressure
                //        ImmutableArray<TypeRef?>.Builder builder = this.typeRefBuilders.Count > 0 ? this.typeRefBuilders.Pop() : ImmutableArray.CreateBuilder<TypeRef?>();

                //        builder.Capacity = (int)count;
                //        for (int i = 0; i < count; i++)
                //        {
                //            builder.Add(itemReader());
                //        }

                //        ImmutableArray<TypeRef?> result = builder.MoveToImmutable();

                //        // Place builder back in cache
                //        this.typeRefBuilders.Push(builder);

                //        return result;
                //    }

                //}
            }
        }

        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [IgnoreMember]
        private string DebuggerDisplay => this.FullName + (this.IsArray ? "[]" : string.Empty);

       // [IgnoreMember]
        private static readonly IEqualityComparer<AssemblyName> AssemblyNameComparer = ByValueEquality.AssemblyNameNoFastCheck;

       //[IgnoreMember]
        private readonly Resolver resolver;

        /// <summary>
        /// Backing field for the lazily initialized <see cref="ResolvedType"/> property.
        /// </summary>
        //[IgnoreMember]
        private Type? resolvedType;

        /// <summary>
        /// A lazily initialized cache of the result of calling <see cref="GetHashCode"/>.
        /// </summary>
       // [IgnoreMember]
        private int? hashCode;

        /// <summary>
        /// Backing field for <see cref="AssemblyId"/>.
        /// </summary>
      //  [IgnoreMember]
        private StrongAssemblyIdentity? assemblyId;

        /// <summary>
        /// Backing field for <see cref="BaseTypes"/>.
        /// </summary>
       // [IgnoreMember]
        private ImmutableArray<TypeRef> baseTypes;

        private TypeRef(
            Resolver resolver,
            AssemblyName assemblyName,
            StrongAssemblyIdentity? assemblyId,
            int metadataToken,
            string fullName,
            TypeRefFlags typeFlags,
            int genericTypeParameterCount,
            ImmutableArray<TypeRef> genericTypeArguments,
            bool shallow,
            ImmutableArray<TypeRef> baseTypes,
            TypeRef? elementTypeRef)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(assemblyName, nameof(assemblyName));
            Requires.Argument(((MetadataTokenType)metadataToken & MetadataTokenType.Mask) == MetadataTokenType.Type, "metadataToken", Strings.NotATypeSpec);
            Requires.Argument(metadataToken != (int)MetadataTokenType.Type, "metadataToken", Strings.UnresolvableMetadataToken);
            Requires.NotNullOrEmpty(fullName, nameof(fullName));

            this.resolver = resolver;
            this.AssemblyName = Resolver.GetNormalizedAssemblyName(assemblyName);
            this.assemblyId = assemblyId;
            this.MetadataToken = metadataToken;
            this.FullName = fullName;
            this.TypeFlags = typeFlags;
            this.GenericTypeParameterCount = genericTypeParameterCount;
            this.GenericTypeArguments = genericTypeArguments;
            if (!shallow)
            {
                this.baseTypes = baseTypes;
            }

            this.ElementTypeRef = elementTypeRef ?? this;
        }


        private TypeRef(Resolver resolver, Type resolvedType, bool shallow = false)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(resolvedType, nameof(resolvedType));

            this.resolver = resolver;
            this.resolvedType = resolvedType;
            this.AssemblyName = resolver.GetNormalizedAssemblyName(resolvedType.GetTypeInfo().Assembly);
            this.assemblyId = resolver.GetStrongAssemblyIdentity(resolvedType.GetTypeInfo().Assembly, this.AssemblyName);
            this.TypeFlags |= resolvedType.IsArray ? TypeRefFlags.Array : TypeRefFlags.None;
            this.TypeFlags |= resolvedType.GetTypeInfo().IsValueType ? TypeRefFlags.IsValueType : TypeRefFlags.None;

            this.ElementTypeRef = PartDiscovery.TryGetElementTypeFromMany(resolvedType, out var elementType)
                ? TypeRef.Get(elementType, resolver)
                : this;

            var arrayElementType = this.ArrayElementType;
            Requires.Argument(!arrayElementType.IsGenericParameter, nameof(resolvedType), "Generic parameters are not supported.");
            this.MetadataToken = arrayElementType.GetTypeInfo().MetadataToken;
            this.FullName = (arrayElementType.GetTypeInfo().IsGenericType ? arrayElementType.GetGenericTypeDefinition() : arrayElementType).FullName ?? throw Assumes.NotReachable();
            this.GenericTypeParameterCount = arrayElementType.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = arrayElementType.GenericTypeArguments != null && arrayElementType.GenericTypeArguments.Length > 0
                ? arrayElementType.GenericTypeArguments.Where(t => !(shallow && t.IsGenericParameter)).Select(t => new TypeRef(resolver, t, shallow: true)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;

            if (!shallow)
            {
                this.baseTypes = arrayElementType.EnumTypeBaseTypesAndInterfaces().Skip(1).Select(t => new TypeRef(resolver, t, shallow: true)).ToImmutableArray();
            }
        }

       // [Key(0)]
        public AssemblyName AssemblyName { get; }

       // [Key(1)]
        public int MetadataToken { get; private set; }

        /// <summary>
        /// Gets the full name of the type represented by this instance.
        /// When representing a generic type, this is the full name of the generic type definition.
        /// </summary>
     //   [Key(2)]
        public string FullName { get; private set; }

      //  [Key(3)]
        public TypeRefFlags TypeFlags { get; }

     //   [Key(4)]
        public bool IsArray => (this.TypeFlags & TypeRefFlags.Array) == TypeRefFlags.Array;

     //   [Key(5)]
        public bool IsValueType => (this.TypeFlags & TypeRefFlags.IsValueType) == TypeRefFlags.IsValueType;

     //   [Key(6)]
        public int GenericTypeParameterCount { get; private set; }

     //   [Key(7)]
        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not this TypeRef is shallow. Shallow TypeRefs do not have a defined list of base types.
        /// </summary>
      //  [Key(8)]
        public bool IsShallow => this.baseTypes.IsDefault;

        /// <summary>
        /// Gets the full list of base types and interfaces for this instance.
        /// </summary>
        /// <remarks>
        /// This list will only be populated if this instance was created with shallow set to false.
        /// The collection is ordered bottom-up for types with the implemented interfaces appended at the end.
        /// </remarks>
    //    [Key(9)]
        public ImmutableArray<TypeRef> BaseTypes
        {
            get
            {
                if (this.IsShallow)
                {
                    throw new InvalidOperationException("Cannot retrieve base types on a shallow TypeRef.");
                }

                return this.baseTypes;
            }
        }

     //   [Key(10)]
        public TypeRef ElementTypeRef { get; private set; }

      //  [Key(11)]
        public bool IsGenericType => this.GenericTypeParameterCount > 0 || this.GenericTypeArguments.Length > 0;

      //  [Key(12)]
        public bool IsGenericTypeDefinition
        {
            get { return this.GenericTypeParameterCount > 0 && this.GenericTypeArguments.Length == 0; }
        }

      //  [Key(13)]
        public StrongAssemblyIdentity AssemblyId
        {
            get
            {
                if (this.assemblyId == null)
                {
                    if (this.Resolver.TryGetAssemblyId(this.AssemblyName, out var assemblyId))
                    {
                        this.assemblyId = assemblyId;
                    }
                    else
                    {
                        this.assemblyId = this.Resolver.GetStrongAssemblyIdentity(this.ResolvedType.GetTypeInfo().Assembly, this.AssemblyName);
                    }
                }

                return this.assemblyId;
            }
        }

        [IgnoreMember]
        internal Resolver Resolver => this.resolver;

        /// <summary>
        /// Gets the resolved type.
        /// </summary>
      //  [IgnoreMember]
        internal Type ResolvedType
        {
            get
            {
                if (this.resolvedType == null)
                {
                    Type type, resolvedType;
                    Module manifest;
                    if (ResolverExtensions.TryUseFastReflection(this, out manifest))
                    {
                        resolvedType = manifest.ResolveType(this.MetadataToken);
                    }
                    else
                    {
                        manifest = this.Resolver.GetManifest(this.AssemblyName);
                        resolvedType = manifest.GetType(this.FullName, throwOnError: true, ignoreCase: false)!;
                    }

                    if (this.GenericTypeArguments.Length > 0)
                    {
                        using (var genericTypeArguments = GetResolvedTypeArray(this.GenericTypeArguments))
                        {
                            type = resolvedType.MakeGenericType(genericTypeArguments.Value);
                        }
                    }
                    else
                    {
                        type = resolvedType;
                    }

                    if (this.IsArray)
                    {
                        type = type.MakeArrayType();
                    }

                    // Only assign the field once we've fully decided what the type is.
                    this.resolvedType = type;
                }

                return this.resolvedType;
            }
        }

      //  [IgnoreMember]
        private Type ArrayElementType => this.IsArray ? this.ResolvedType.GetElementType()! : this.ResolvedType;

        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, string fullName, TypeRefFlags typeFlags, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, bool shallow, ImmutableArray<TypeRef> baseTypes, TypeRef? elementTypeRef)
        {
            Requires.NotNull(resolver, nameof(resolver));
            return new TypeRef(resolver, Resolver.GetNormalizedAssemblyName(assemblyName), null, metadataToken, fullName, typeFlags, genericTypeParameterCount, genericTypeArguments, shallow, baseTypes, elementTypeRef);
        }

        public static TypeRef Get(Resolver resolver, StrongAssemblyIdentity assemblyId, int metadataToken, string fullName, TypeRefFlags typeFlags, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, bool shallow, ImmutableArray<TypeRef> baseTypes, TypeRef? elementTypeRef)
        {
            return new TypeRef(resolver, assemblyId.Name, assemblyId, metadataToken, fullName, typeFlags, genericTypeParameterCount, genericTypeArguments, shallow, baseTypes, elementTypeRef);
        }

        /// <summary>
        /// Gets a TypeRef that represents a given Type instance.
        /// </summary>
        /// <param name="type">The Type to represent. May be <see langword="null"/> to get a <see langword="null"/> result.</param>
        /// <param name="resolver">The resolver to use to reconstitute <paramref name="type"/> or derivatives later.</param>
        /// <returns>An instance of TypeRef if <paramref name="type"/> is not <see langword="null"/>; otherwise <see langword="null"/>.</returns>
        [return: NotNullIfNotNull("type")]
        public static TypeRef? Get(Type? type, Resolver resolver)
        {
            Requires.NotNull(resolver, nameof(resolver));

            if (type == null)
            {
                return null;
            }

            TypeRef? result;
            lock (resolver.InstanceCache)
            {
                WeakReference<TypeRef>? weakResult;
                if (!resolver.InstanceCache.TryGetValue(type, out weakResult))
                {
                    result = new TypeRef(resolver, type);
                    resolver.InstanceCache.Add(type, new WeakReference<TypeRef>(result));
                }
                else
                {
                    if (!weakResult.TryGetTarget(out result))
                    {
                        result = new TypeRef(resolver, type);
                        weakResult.SetTarget(result);
                    }
                }
            }

            Debug.Assert(type.IsEquivalentTo(result.Resolve()), "Type reference failed to resolve to the original type.");

            return result;
        }

        public TypeRef MakeGenericTypeRef(ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.Argument(!genericTypeArguments.IsDefault, "genericTypeArguments", Strings.NotInitialized);
            Verify.Operation(this.IsGenericTypeDefinition, Strings.NotGenericTypeDefinition);

            // We use the resolver parameter instead of the field here because this TypeRef instance
            // might have been constructed by TypeRef.Get(Type) and thus not have a resolver.
            return new TypeRef(this.Resolver, this.AssemblyName, this.assemblyId, this.MetadataToken, this.FullName, this.TypeFlags, this.GenericTypeParameterCount, genericTypeArguments, this.IsShallow, this.BaseTypes, this.ElementTypeRef);
        }

        /// <summary>
        /// Checks if the type represented by the given TypeRef can be assigned to the type represented by this instance.
        /// </summary>
        /// <remarks>
        /// The assignability check is done by traversing all the base types and interfaces of the given TypeRef to check
        /// if any of them are equal to this instance. Should that fail, the CLR is asked to check for assignability
        /// which will trigger an assembly load.
        /// </remarks>
        /// <param name="other">TypeRef to compare to.</param>
        /// <returns>true if the given TypeRef can be assigned to this instance, false otherwise.</returns>
        public bool IsAssignableFrom(TypeRef other)
        {
            if (other == null)
            {
                return false;
            }

            if (this.TypeRefEquals(other))
            {
                return true;
            }

            foreach (var baseType in other.BaseTypes)
            {
                if (this.TypeRefEquals(baseType))
                {
                    return true;
                }
            }

            return this.ResolvedType.GetTypeInfo().IsAssignableFrom(other.ResolvedType.GetTypeInfo());
        }

        public override int GetHashCode()
        {
            if (!this.hashCode.HasValue)
            {
                this.hashCode = AssemblyNameComparer.GetHashCode(this.AssemblyName) + this.MetadataToken;
            }

            return this.hashCode.Value;
        }

        /// <summary>
        /// Compares for type equality, ignoring whether or not the TypeRef is shallow.
        /// </summary>
        /// <param name="other">TypeRef to compare to.</param>
        /// <returns>true if the TypeRefs represent the same type, false otherwise.</returns>
        internal bool TypeRefEquals(TypeRef other)
        {
            if (other == null)
            {
                return false;
            }

            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            bool result = this.MetadataToken == other.MetadataToken
                && AssemblyNameComparer.Equals(this.AssemblyName, other.AssemblyName)
                && this.IsArray == other.IsArray
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments);
            return result;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypeRef typeRef && this.Equals(typeRef);
        }

        public bool Equals(TypeRef? other) => this.Equals(other, allowMvidMismatch: false);

        public bool Equals(Type? other)
        {
            // We allow MVID mismatches in this overload because at this point one of the types are already loaded,
            // and the CLR doesn't look at the MVID for type equivalence. Our only caller (as of now) is looking for a matching signature
            // so requiring an MVID match would cause the appropriate overload to be missed.
            return this.Equals(TypeRef.Get(other, this.Resolver), allowMvidMismatch: true);
        }

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies) => ResolverExtensions.GetInputAssemblies(this, assemblies);

        private static Rental<Type[]> GetResolvedTypeArray(ImmutableArray<TypeRef> typeRefs)
        {
            if (typeRefs.IsDefault)
            {
                return default(Rental<Type[]>);
            }

            var result = ArrayRental<Type>.Get(typeRefs.Length);
            for (int i = 0; i < typeRefs.Length; i++)
            {
                result.Value[i] = typeRefs[i].ResolvedType;
            }

            return result;
        }

        private static Type[] GetGenericTypeArguments(MemberInfo member)
        {
            if (member is TypeInfo typeInfo)
            {
                return typeInfo.GetGenericArguments();
            }
            else if (member is MethodInfo methodInfo)
            {
                return methodInfo.GetGenericArguments();
            }
            else
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException();
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }
        }

        private bool Equals(TypeRef? other, bool allowMvidMismatch)
        {
            if (other == null)
            {
                return false;
            }

            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            bool result = this.MetadataToken == other.MetadataToken
                && this.AssemblyId.Equals(other.AssemblyId, allowMvidMismatch)
                && this.IsArray == other.IsArray
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && EqualsByValue(this.GenericTypeArguments, other.GenericTypeArguments, allowMvidMismatch)
                && this.IsShallow == other.IsShallow;
            return result;

            static bool EqualsByValue(ImmutableArray<TypeRef> array, ImmutableArray<TypeRef> other, bool allowMvidMismatch)
            {
                if (array.Length != other.Length)
                {
                    return false;
                }

                for (int i = 0; i < array.Length; i++)
                {
                    if (!array[i].Equals(other[i], allowMvidMismatch))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
