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
    using System.Text;
    using System.Threading.Tasks;

    [DebuggerDisplay("{" + nameof(ResolvedType) + ".FullName,nq}")]
    public class TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
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

        private TypeRef(
            Resolver resolver,
            AssemblyName assemblyName,
            int metadataToken,
            string fullName,
            bool isArray,
            int genericTypeParameterCount,
            ImmutableArray<TypeRef> genericTypeArguments,
            MemberRef declaringMember,
            int declaringMethodParameterIndex)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(assemblyName, nameof(assemblyName));
            Requires.Argument(((MetadataTokenType)metadataToken & MetadataTokenType.Mask) == MetadataTokenType.Type, "metadataToken", Strings.NotATypeSpec);
            Requires.Argument(metadataToken != (int)MetadataTokenType.Type, "metadataToken", Strings.UnresolvableMetadataToken);
            Requires.NotNullOrEmpty(fullName, nameof(fullName));

            this.resolver = resolver;
            this.AssemblyName = GetNormalizedAssemblyName(assemblyName);
            this.MetadataToken = metadataToken;
            this.FullName = fullName;
            this.IsArray = isArray;
            this.GenericTypeParameterCount = genericTypeParameterCount;
            this.GenericTypeArguments = genericTypeArguments;
            this.GenericParameterDeclaringMemberRef = declaringMember;
            this.GenericParameterDeclaringMemberIndex = declaringMethodParameterIndex;
        }

        private TypeRef(Resolver resolver, Type type)
        {
            Requires.NotNull(resolver, nameof(resolver));
            Requires.NotNull(type, nameof(type));

            this.resolver = resolver;
            this.AssemblyName = GetNormalizedAssemblyName(type.GetTypeInfo().Assembly.GetName());
            this.IsArray = type.IsArray;

            Type elementType = type.IsArray ? type.GetElementType() : type;
            this.MetadataToken = elementType.GetTypeInfo().MetadataToken;
            this.FullName = (elementType.GetTypeInfo().IsGenericType ? elementType.GetGenericTypeDefinition() : elementType).FullName;
            this.GenericTypeParameterCount = elementType.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = elementType.GenericTypeArguments != null && elementType.GenericTypeArguments.Length > 0
                ? elementType.GenericTypeArguments.Select(t => new TypeRef(resolver, t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;

            if (elementType.IsGenericParameter)
            {
                // Generic type parameters may come in without be type specs. So the only way to reconstruct them is by way of who references them.
                var declaringMember = (MemberInfo)elementType.GetTypeInfo().DeclaringMethod ?? elementType.DeclaringType.GetTypeInfo();
                this.GenericParameterDeclaringMemberRef = MemberRef.Get(declaringMember, resolver);
                this.GenericParameterDeclaringMemberIndex = Array.IndexOf(GetGenericTypeArguments(declaringMember), elementType);
            }
        }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken { get; private set; }

        /// <summary>
        /// Gets the full name of the type represented by this instance.
        /// When representing a generic type, this is the full name of the generic type definition.
        /// </summary>
        public string FullName { get; private set; }

        public bool IsArray { get; private set; }

        public int GenericTypeParameterCount { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        public MemberRef GenericParameterDeclaringMemberRef { get; private set; }

        public int GenericParameterDeclaringMemberIndex { get; private set; }

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
                    if (((MetadataTokenType)this.MetadataToken & MetadataTokenType.Mask) == MetadataTokenType.Type)
                    {
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
                    }
                    else
                    {
                        MemberInfo declaringMember = this.GenericParameterDeclaringMemberRef.Resolve();
                        Type[] genericTypeArgs = GetGenericTypeArguments(declaringMember);
                        type = genericTypeArgs[this.GenericParameterDeclaringMemberIndex];
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

        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, string fullName, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            return new TypeRef(resolver, assemblyName, metadataToken, fullName, isArray, genericTypeParameterCount, genericTypeArguments, default(MemberRef), 0);
        }

        public static TypeRef Get(Resolver resolver, AssemblyName assemblyName, int metadataToken, string fullName, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, MemberRef declaringMember, int declaringMethodParameterIndex = 0)
        {
            return new TypeRef(resolver, assemblyName, metadataToken, fullName, isArray, genericTypeParameterCount, genericTypeArguments, declaringMember, declaringMethodParameterIndex);
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
            return new TypeRef(this.Resolver, this.AssemblyName, this.MetadataToken, this.FullName, this.IsArray, this.GenericTypeParameterCount, genericTypeArguments, default(MemberRef), 0);
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
            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            bool result = this.MetadataToken == other.MetadataToken
                && AssemblyNameComparer.Equals(this.AssemblyName, other.AssemblyName)
                && this.IsArray == other.IsArray
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments)
                && this.GenericParameterDeclaringMemberRef.Equals(other.GenericParameterDeclaringMemberRef)
                && this.GenericParameterDeclaringMemberIndex == other.GenericParameterDeclaringMemberIndex;
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
