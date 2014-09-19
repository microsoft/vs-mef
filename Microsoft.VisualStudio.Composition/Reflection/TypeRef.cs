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
    using Validation;

    [DebuggerDisplay("{ResolvedType.FullName,nq}")]
    public class TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
        /// <summary>
        /// A cache of TypeRef instances that correspond to Type instances.
        /// </summary>
        /// <remarks>
        /// This is for efficiency to avoid duplicates where convenient to do so.
        /// It is not intended as a guarantee of reference equality across equivalent TypeRef instances.
        /// </remarks>
        private static readonly Dictionary<Type, WeakReference<TypeRef>> instanceCache = new Dictionary<Type, WeakReference<TypeRef>>();

        /// <summary>
        /// A cache of normalized AssemblyNames with any CodeBase using 8.3 short names expanded.
        /// </summary>
        private static readonly Dictionary<string, AssemblyName> assemblyNameCache = new Dictionary<string, AssemblyName>();

        /// <summary>
        /// Backing field for the lazily initialized <see cref="ResolvedType"/> property.
        /// </summary>
        private Type resolvedType;

        /// <summary>
        /// A lazily initialized cache of the result of calling <see cref="GetHashCode"/>.
        /// </summary>
        private int? hashCode;

        private TypeRef(AssemblyName assemblyName, int metadataToken, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, MemberRef declaringMember, int declaringMethodParameterIndex)
        {
            Requires.NotNull(assemblyName, "assemblyName");
            Requires.Argument(((MetadataTokenType)metadataToken & MetadataTokenType.Mask) == MetadataTokenType.Type, "metadataToken", "Not a type spec.");
            Requires.Argument(metadataToken != (int)MetadataTokenType.Type, "metadataToken", "Unresolvable metadata token.");

            this.AssemblyName = GetNormalizedAssemblyName(assemblyName);
            this.MetadataToken = metadataToken;
            this.IsArray = isArray;
            this.GenericTypeParameterCount = genericTypeParameterCount;
            this.GenericTypeArguments = genericTypeArguments;
            this.GenericParameterDeclaringMember = declaringMember;
            this.GenericParameterDeclaringMemberIndex = declaringMethodParameterIndex;
        }

        private TypeRef(Type type)
        {
            Requires.NotNull(type, "type");

            this.AssemblyName = GetNormalizedAssemblyName(type.Assembly.GetName());
            this.IsArray = type.IsArray;

            Type elementType = type.IsArray ? type.GetElementType() : type;
            this.MetadataToken = elementType.MetadataToken;
            this.GenericTypeParameterCount = elementType.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = elementType.GenericTypeArguments != null && elementType.GenericTypeArguments.Length > 0
                ? elementType.GenericTypeArguments.Select(t => new TypeRef(t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;

            if (elementType.IsGenericParameter)
            {
                // Generic type parameters may come in without be type specs. So the only way to reconstruct them is by way of who references them.
                var declaringMember = (MemberInfo)elementType.DeclaringMethod ?? elementType.DeclaringType;
                this.GenericParameterDeclaringMember = MemberRef.Get(declaringMember);
                this.GenericParameterDeclaringMemberIndex = Array.IndexOf(GetGenericTypeArguments(declaringMember), elementType);
            }
        }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken { get; private set; }

        public bool IsArray { get; private set; }

        public int GenericTypeParameterCount { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        public MemberRef GenericParameterDeclaringMember { get; private set; }

        public int GenericParameterDeclaringMemberIndex { get; private set; }

        public bool IsGenericTypeDefinition
        {
            get { return this.GenericTypeParameterCount > 0 && this.GenericTypeArguments.Length == 0; }
        }

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
                        var manifest = Resolver.GetManifest(this.AssemblyName);
                        var resolvedType = manifest.ResolveType(this.MetadataToken);
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
                        MemberInfo declaringMember = this.GenericParameterDeclaringMember.Resolve();
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

        public static TypeRef Get(AssemblyName assemblyName, int metadataToken, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            return new TypeRef(assemblyName, metadataToken, isArray, genericTypeParameterCount, genericTypeArguments, default(MemberRef), -1);
        }

        public static TypeRef Get(AssemblyName assemblyName, int metadataToken, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments, MemberRef declaringMember, int declaringMethodParameterIndex = -1)
        {
            return new TypeRef(assemblyName, metadataToken, isArray, genericTypeParameterCount, genericTypeArguments, declaringMember, declaringMethodParameterIndex);
        }

        /// <summary>
        /// Gets a TypeRef that represents a given Type instance.
        /// </summary>
        /// <param name="type">The Type to represent. May be <c>null</c> to get a <c>null</c> result.</param>
        /// <returns>An instance of TypeRef if <paramref name="type"/> is not <c>null</c>; otherwise <c>null</c>.</returns>
        public static TypeRef Get(Type type)
        {
            if (type == null)
            {
                return null;
            }

            TypeRef result;
            lock (instanceCache)
            {
                WeakReference<TypeRef> weakResult;
                if (!instanceCache.TryGetValue(type, out weakResult))
                {
                    result = new TypeRef(type);
                    instanceCache.Add(type, new WeakReference<TypeRef>(result));
                }
                else
                {
                    if (!weakResult.TryGetTarget(out result))
                    {
                        result = new TypeRef(type);
                        weakResult.SetTarget(result);
                    }
                }
            }

            //Debug.Assert(type.IsEquivalentTo(result.Resolve()));

            return result;
        }

        public TypeRef MakeGenericType(ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.Argument(!genericTypeArguments.IsDefault, "genericTypeArguments", "Not initialized.");
            Verify.Operation(this.IsGenericTypeDefinition, "This is not a generic type definition.");
            return new Reflection.TypeRef(this.AssemblyName, this.MetadataToken, this.IsArray, this.GenericTypeParameterCount, genericTypeArguments, default(MemberRef), -1);
        }

        public override int GetHashCode()
        {
            if (!this.hashCode.HasValue)
            {
                this.hashCode = ByValueEquality.AssemblyName.GetHashCode(this.AssemblyName) + this.MetadataToken;
            }

            return this.hashCode.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRef && this.Equals((TypeRef)obj);
        }

        public bool Equals(TypeRef other)
        {
            return ByValueEquality.AssemblyName.Equals(this.AssemblyName, other.AssemblyName)
                && this.MetadataToken == other.MetadataToken
                && this.IsArray == other.IsArray
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments);
        }

        public bool Equals(Type other)
        {
            if (other.ContainsGenericParameters)
            {
                return this.Resolve().IsEquivalentTo(other);
            }
            else
            {
                return this.Equals(TypeRef.Get(other));
            }
        }

        private static Rental<Type[]> GetResolvedTypeArray(ImmutableArray<TypeRef> typeRefs)
        {
            if (typeRefs.IsDefault)
            {
                return new Rental<Type[]>();
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
            if (member is Type)
            {
                return ((Type)member).GetGenericArguments();
            }
            else if (member is MethodInfo)
            {
                return ((MethodInfo)member).GetGenericArguments();
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private static AssemblyName GetNormalizedAssemblyName(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, "assemblyName");

            AssemblyName normalizedAssemblyName;
            lock (assemblyNameCache)
            {
                assemblyNameCache.TryGetValue(assemblyName.FullName, out normalizedAssemblyName);
            }

            if (normalizedAssemblyName == null)
            {
                normalizedAssemblyName = assemblyName;
                if (assemblyName.CodeBase.IndexOf('~') >= 0)
                {
                    // Using ToString() rather than AbsoluteUri here to match the CLR's AssemblyName.CodeBase convention of paths without %20 space characters.
                    string normalizedCodeBase = new Uri(Path.GetFullPath(new Uri(assemblyName.CodeBase).LocalPath)).ToString();
                    normalizedAssemblyName = new AssemblyName(assemblyName.FullName);
                    normalizedAssemblyName.CodeBase = normalizedCodeBase;
                }

                lock (assemblyNameCache)
                {
                    assemblyNameCache[assemblyName.FullName] = normalizedAssemblyName;
                }
            }

            return normalizedAssemblyName;
        }
    }
}
