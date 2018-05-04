// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
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
        private Type resolvedType;

        /// <summary>
        /// A lazily initialized cache of the result of calling <see cref="GetHashCode"/>.
        /// </summary>
        private int? hashCode;

        /// <summary>
        /// Backing field for <see cref="AssemblyId"/>.
        /// </summary>
        private StrongAssemblyIdentity assemblyId;

        private TypeRef(
            Resolver resolver,
            AssemblyName assemblyName,
            StrongAssemblyIdentity assemblyId,
            int metadataToken,
            string fullName,
            TypeRefFlags typeFlags,
            int genericTypeParameterCount,
            ImmutableArray<TypeRef> genericTypeArguments)
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
        }

        private TypeRef(Resolver resolver, Type type)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(type, nameof(type));

            this.resolver = resolver;
            this.resolvedType = type;
            this.AssemblyName = GetNormalizedAssemblyName(type.GetTypeInfo().Assembly.GetName());
            this.assemblyId = resolver.GetStrongAssemblyIdentity(type.GetTypeInfo().Assembly, this.AssemblyName);
            this.TypeFlags |= type.IsArray ? TypeRefFlags.Array : TypeRefFlags.None;
            this.TypeFlags |= type.GetTypeInfo().IsValueType ? TypeRefFlags.IsValueType : TypeRefFlags.None;

            Type elementType = this.ElementType;
            Requires.Argument(!elementType.IsGenericParameter, nameof(type), "Generic parameters are not supported.");
            this.MetadataToken = elementType.GetTypeInfo().MetadataToken;
            this.FullName = (elementType.GetTypeInfo().IsGenericType ? elementType.GetGenericTypeDefinition() : elementType).FullName;
            this.GenericTypeParameterCount = elementType.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = elementType.GenericTypeArguments != null && elementType.GenericTypeArguments.Length > 0
                ? elementType.GenericTypeArguments.Select(t => new TypeRef(resolver, t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;
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
#if RuntimeHandles
                    if (ResolverExtensions.TryUseFastReflection(this, out manifest))
                    {
                        resolvedType = manifest.ResolveType(this.MetadataToken);
                    }
                    else
#endif
                    {
                        manifest = this.Resolver.GetManifest(this.AssemblyName);
                        resolvedType = manifest.GetType(this.FullName);
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

        private Type ElementType => this.IsArray ? this.ResolvedType.GetElementType() : this.ResolvedType;

        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, string fullName, TypeRefFlags typeFlags, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.NotNull(resolver, nameof(resolver));
            return new TypeRef(resolver, assemblyName, null, metadataToken, fullName, typeFlags, genericTypeParameterCount, genericTypeArguments);
        }

        public static TypeRef Get(Resolver resolver, StrongAssemblyIdentity assemblyId, int metadataToken, string fullName, TypeRefFlags typeFlags, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            return new TypeRef(resolver, assemblyId.Name, assemblyId, metadataToken, fullName, typeFlags, genericTypeParameterCount, genericTypeArguments);
        }

        /// <summary>
        /// Gets a TypeRef that represents a given Type instance.
        /// </summary>
        /// <param name="type">The Type to represent. May be <c>null</c> to get a <c>null</c> result.</param>
        /// <param name="resolver">The resolver to use to reconstitute <paramref name="type"/> or derivatives later.</param>
        /// <returns>An instance of TypeRef if <paramref name="type"/> is not <c>null</c>; otherwise <c>null</c>.</returns>
        public static TypeRef Get(Type type, Resolver resolver)
        {
            Requires.NotNull(resolver, nameof(resolver));

            if (type == null)
            {
                return null;
            }

            TypeRef result;
            lock (resolver.InstanceCache)
            {
                WeakReference<TypeRef> weakResult;
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
            return new TypeRef(this.Resolver, this.AssemblyName, this.assemblyId, this.MetadataToken, this.FullName, this.TypeFlags, this.GenericTypeParameterCount, genericTypeArguments);
        }

        public override int GetHashCode()
        {
            if (!this.hashCode.HasValue)
            {
                this.hashCode = AssemblyNameComparer.GetHashCode(this.AssemblyName) + this.MetadataToken;
            }

            return this.hashCode.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRef typeRef && this.Equals(typeRef);
        }

        public bool Equals(TypeRef other)
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

        public bool Equals(Type other)
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
                throw new ArgumentException();
            }
        }

        private static AssemblyName GetNormalizedAssemblyName(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, nameof(assemblyName));

            AssemblyName normalizedAssemblyName = assemblyName;
#if DESKTOP
            if (assemblyName.CodeBase.IndexOf('~') >= 0)
            {
                // Using ToString() rather than AbsoluteUri here to match the CLR's AssemblyName.CodeBase convention of paths without %20 space characters.
                string normalizedCodeBase = new Uri(Path.GetFullPath(new Uri(assemblyName.CodeBase).LocalPath)).ToString();
                normalizedAssemblyName = (AssemblyName)assemblyName.Clone();
                normalizedAssemblyName.CodeBase = normalizedCodeBase;
            }
#endif

            return normalizedAssemblyName;
        }
    }
}
