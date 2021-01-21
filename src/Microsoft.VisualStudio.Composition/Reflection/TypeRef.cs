// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => this.FullName + (this.IsArray ? "[]" : string.Empty);

        private static readonly IEqualityComparer<AssemblyName> AssemblyNameComparer = ByValueEquality.AssemblyNameNoFastCheck;

        private readonly Resolver resolver;

        /// <summary>
        /// Backing field for the lazily initialized <see cref="ResolvedType"/> property.
        /// </summary>
        private Type? resolvedType;

        /// <summary>
        /// A lazily initialized cache of the result of calling <see cref="GetHashCode"/>.
        /// </summary>
        private int? hashCode;

        /// <summary>
        /// Backing field for <see cref="AssemblyId"/>.
        /// </summary>
        private StrongAssemblyIdentity? assemblyId;

        /// <summary>
        /// Backing field for <see cref="BaseTypes"/>.
        /// </summary>
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
            this.AssemblyName = GetNormalizedAssemblyName(assemblyName);
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

        private TypeRef(Resolver resolver, Type type, bool shallow = false)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(type, nameof(type));

            this.resolver = resolver;
            this.resolvedType = type;
            this.AssemblyName = GetNormalizedAssemblyName(type.GetTypeInfo().Assembly.GetName());
            this.assemblyId = resolver.GetStrongAssemblyIdentity(type.GetTypeInfo().Assembly, this.AssemblyName);
            this.TypeFlags |= type.IsArray ? TypeRefFlags.Array : TypeRefFlags.None;
            this.TypeFlags |= type.GetTypeInfo().IsValueType ? TypeRefFlags.IsValueType : TypeRefFlags.None;

            this.ElementTypeRef = PartDiscovery.TryGetElementTypeFromMany(type, out var elementType)
                ? TypeRef.Get(elementType, resolver)
                : this;

            var arrayElementType = this.ArrayElementType;
            Requires.Argument(!arrayElementType.IsGenericParameter, nameof(type), "Generic parameters are not supported.");
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

        public AssemblyName AssemblyName { get; }

        public int MetadataToken { get; private set; }

        /// <summary>
        /// Gets the full name of the type represented by this instance.
        /// When representing a generic type, this is the full name of the generic type definition.
        /// </summary>
        public string FullName { get; private set; }

        public TypeRefFlags TypeFlags { get; }

        public bool IsArray => (this.TypeFlags & TypeRefFlags.Array) == TypeRefFlags.Array;

        public bool IsValueType => (this.TypeFlags & TypeRefFlags.IsValueType) == TypeRefFlags.IsValueType;

        public int GenericTypeParameterCount { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not this TypeRef is shallow. Shallow TypeRefs do not have a defined list of base types.
        /// </summary>
        public bool IsShallow => this.baseTypes.IsDefault;

        /// <summary>
        /// Gets the full list of base types and interfaces for this instance.
        /// </summary>
        /// <remarks>
        /// This list will only be populated if this instance was created with shallow set to false.
        /// The collection is ordered bottom-up for types with the implemented interfaces appended at the end.
        /// </remarks>
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

        public TypeRef ElementTypeRef { get; private set; }

        public bool IsGenericType => this.GenericTypeParameterCount > 0 || this.GenericTypeArguments.Length > 0;

        public bool IsGenericTypeDefinition
        {
            get { return this.GenericTypeParameterCount > 0 && this.GenericTypeArguments.Length == 0; }
        }

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

        internal Resolver Resolver => this.resolver;

        /// <summary>
        /// Gets the resolved type.
        /// </summary>
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

        private Type ArrayElementType => this.IsArray ? this.ResolvedType.GetElementType()! : this.ResolvedType;

        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, string fullName, TypeRefFlags typeFlags, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, bool shallow, ImmutableArray<TypeRef> baseTypes, TypeRef? elementTypeRef)
        {
            Requires.NotNull(resolver, nameof(resolver));
            return new TypeRef(resolver, assemblyName, null, metadataToken, fullName, typeFlags, genericTypeParameterCount, genericTypeArguments, shallow, baseTypes, elementTypeRef);
        }

        public static TypeRef Get(Resolver resolver, StrongAssemblyIdentity assemblyId, int metadataToken, string fullName, TypeRefFlags typeFlags, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, bool shallow, ImmutableArray<TypeRef> baseTypes, TypeRef? elementTypeRef)
        {
            return new TypeRef(resolver, assemblyId.Name, assemblyId, metadataToken, fullName, typeFlags, genericTypeParameterCount, genericTypeArguments, shallow, baseTypes, elementTypeRef);
        }

        /// <summary>
        /// Gets a TypeRef that represents a given Type instance.
        /// </summary>
        /// <param name="type">The Type to represent. May be <c>null</c> to get a <c>null</c> result.</param>
        /// <param name="resolver">The resolver to use to reconstitute <paramref name="type"/> or derivatives later.</param>
        /// <returns>An instance of TypeRef if <paramref name="type"/> is not <c>null</c>; otherwise <c>null</c>.</returns>
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

        public bool Equals(TypeRef? other)
        {
            if (other == null)
            {
                return false;
            }

            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            bool result = this.MetadataToken == other.MetadataToken
                && this.AssemblyId.Equals(other.AssemblyId)
                && this.IsArray == other.IsArray
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments)
                && this.IsShallow == other.IsShallow;
            return result;
        }

        public bool Equals(Type? other)
        {
            return this.Equals(TypeRef.Get(other, this.Resolver));
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

        private static AssemblyName GetNormalizedAssemblyName(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, nameof(assemblyName));

            AssemblyName normalizedAssemblyName = assemblyName;
            if (assemblyName.CodeBase?.IndexOf('~') >= 0)
            {
                // Using ToString() rather than AbsoluteUri here to match the CLR's AssemblyName.CodeBase convention of paths without %20 space characters.
                string normalizedCodeBase = new Uri(Path.GetFullPath(new Uri(assemblyName.CodeBase).LocalPath)).ToString();
                normalizedAssemblyName = (AssemblyName)assemblyName.Clone();
                normalizedAssemblyName.CodeBase = normalizedCodeBase;
            }

            return normalizedAssemblyName;
        }
    }
}
