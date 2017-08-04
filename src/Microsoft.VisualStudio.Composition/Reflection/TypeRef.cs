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

    [DebuggerDisplay("{" + nameof(ResolvedType) + ".FullName,nq}")]
    public class TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
        private static readonly IEqualityComparer<AssemblyName> AssemblyNameComparer = ByValueEquality.AssemblyNameNoFastCheck;
        private readonly Resolver resolver;

        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? metadataToken;

        /// <summary>
        /// Backing field for the lazily initialized <see cref="ResolvedType"/> property.
        /// </summary>
        private Type resolvedType;

        /// <summary>
        /// A lazily initialized cache of the result of calling <see cref="GetHashCode"/>.
        /// </summary>
        private int? hashCode;

        private TypeRef(
            Resolver resolver,
            AssemblyName assemblyName,
            int metadataToken,
            string fullName,
            bool isArray,
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
            this.metadataToken = metadataToken;
            this.FullName = fullName;
            this.IsArray = isArray;
            this.GenericTypeParameterCount = genericTypeParameterCount;
            this.GenericTypeArguments = genericTypeArguments;
        }

        private TypeRef(Resolver resolver, Type type, AssemblyName assemblyName = null)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(type, nameof(type));

            this.resolver = resolver;
            this.resolvedType = type;
            this.AssemblyName = assemblyName ?? GetNormalizedAssemblyName(type.GetTypeInfo().Assembly.GetName());
            this.IsArray = type.IsArray;

            Type elementType = this.ElementType;
            Requires.Argument(!elementType.IsGenericParameter, nameof(type), "Generic parameters are not supported.");
            this.FullName = (elementType.GetTypeInfo().IsGenericType ? elementType.GetGenericTypeDefinition() : elementType).FullName;
            this.GenericTypeParameterCount = elementType.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = elementType.GenericTypeArguments != null && elementType.GenericTypeArguments.Length > 0
                ? elementType.GenericTypeArguments.Select(t => new TypeRef(resolver, t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;
        }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken
        {
            get
            {
                if (this.metadataToken.HasValue)
                {
                    return this.metadataToken.Value;
                }

#if DESKTOP
                // Avoid calling TypeInfo.MetadataToken on TypeBuilders because they throw exceptions
                if (this.ElementType is System.Reflection.Emit.TypeBuilder tb)
                {
                    return tb.TypeToken.Token;
                }
#endif

                return this.ElementType.GetTypeInfo().MetadataToken;
            }
        }

        /// <summary>
        /// Gets the full name of the type represented by this instance.
        /// When representing a generic type, this is the full name of the generic type definition.
        /// </summary>
        public string FullName { get; private set; }

        public bool IsArray { get; private set; }

        public int GenericTypeParameterCount { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        public bool IsGenericTypeDefinition
        {
            get { return this.GenericTypeParameterCount > 0 && this.GenericTypeArguments.Length == 0; }
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
                    Type type;
                    var manifest = this.Resolver.GetManifest(this.AssemblyName);
#if RuntimeHandles
                    var resolvedType = manifest.ResolveType(this.MetadataToken);
#else
                    var resolvedType = manifest.GetType(this.FullName);
#endif
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

        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, string fullName, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            return new TypeRef(resolver, assemblyName, metadataToken, fullName, isArray, genericTypeParameterCount, genericTypeArguments);
        }

        /// <summary>
        /// Gets a TypeRef that represents a given Type instance.
        /// </summary>
        /// <param name="type">The Type to represent. May be <c>null</c> to get a <c>null</c> result.</param>
        /// <param name="resolver">The resolver to use to reconstitute <paramref name="type"/> or derivatives later.</param>
        /// <returns>An instance of TypeRef if <paramref name="type"/> is not <c>null</c>; otherwise <c>null</c>.</returns>
        public static TypeRef Get(Type type, Resolver resolver) => Get(type, resolver, null);

        /// <summary>
        /// Gets a TypeRef that represents a given Type instance.
        /// </summary>
        /// <param name="type">The Type to represent. May be <c>null</c> to get a <c>null</c> result.</param>
        /// <param name="resolver">The resolver to use to reconstitute <paramref name="type"/> or derivatives later.</param>
        /// <param name="assemblyName">An optional assembly name to consider that created the Type.</param>
        /// <returns>An instance of TypeRef if <paramref name="type"/> is not <c>null</c>; otherwise <c>null</c>.</returns>
        internal static TypeRef Get(Type type, Resolver resolver, AssemblyName assemblyName)
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
                    result = new TypeRef(resolver, type, assemblyName);
                    resolver.InstanceCache.Add(type, new WeakReference<TypeRef>(result));
                }
                else
                {
                    if (!weakResult.TryGetTarget(out result))
                    {
                        result = new TypeRef(resolver, type, assemblyName);
                        weakResult.SetTarget(result);
                    }
                }
            }

            Debug.Assert(type.IsEquivalentTo(result.Resolve()), "Type reference failed to resolve to the original type.");

            return result;
        }

#if NET45
        [Obsolete]
        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            Type type = resolver.AssemblyLoader.LoadAssembly(assemblyName).ManifestModule.ResolveType(metadataToken);
            return Get(type, resolver);
        }

        [Obsolete]
        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, MemberRef declaringMember, int declaringMethodParameterIndex = 0)
        {
            Type type = resolver.AssemblyLoader.LoadAssembly(assemblyName).ManifestModule.ResolveType(metadataToken);
            return Get(type, resolver);
        }
#endif

        public TypeRef MakeGenericTypeRef(ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.Argument(!genericTypeArguments.IsDefault, "genericTypeArguments", Strings.NotInitialized);
            Verify.Operation(this.IsGenericTypeDefinition, Strings.NotGenericTypeDefinition);

            // We use the resolver parameter instead of the field here because this TypeRef instance
            // might have been constructed by TypeRef.Get(Type) and thus not have a resolver.
            return new TypeRef(this.Resolver, this.AssemblyName, this.MetadataToken, this.FullName, this.IsArray, this.GenericTypeParameterCount, genericTypeArguments);
        }

        public override int GetHashCode()
        {
            if (!this.hashCode.HasValue)
            {
                int hashCode = AssemblyNameComparer.GetHashCode(this.AssemblyName);
                hashCode += this.FullName.GetHashCode();
                this.hashCode = hashCode;
            }

            return this.hashCode.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRef typeRef && this.Equals(typeRef);
        }

        public bool Equals(TypeRef other)
        {
            if (this.resolvedType != null && other.resolvedType != null)
            {
                return this.resolvedType.IsEquivalentTo(other.resolvedType);
            }
            else if (this.metadataToken.HasValue && other.metadataToken.HasValue)
            {
                if (this.metadataToken.Value != other.metadataToken.Value)
                {
                    return false;
                }
            }
            else if (this.FullName != other.FullName)
            {
                return false;
            }

            bool result = AssemblyNameComparer.Equals(this.AssemblyName, other.AssemblyName)
                && this.IsArray == other.IsArray
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments);
            return result;
        }

        public bool Equals(Type other)
        {
            return this.Equals(TypeRef.Get(other, this.Resolver));
        }

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
#if NET45
            if (assemblyName.CodeBase != null && assemblyName.CodeBase.IndexOf('~') >= 0)
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
