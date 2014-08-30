namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
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
        /// Backing field for the lazily initialized <see cref="ResolvedType"/> property.
        /// </summary>
        private Type resolvedType;

        private TypeRef(AssemblyName assemblyName, int metadataToken, bool isArray, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.NotNull(assemblyName, "assemblyName");
            Requires.Argument(metadataToken != 0x02000000, "metadataToken", "Unresolvable metadata token.");

            this.AssemblyName = assemblyName;
            this.MetadataToken = metadataToken;
            this.IsArray = isArray;
            this.GenericTypeParameterCount = genericTypeParameterCount;
            this.GenericTypeArguments = genericTypeArguments;
        }

        private TypeRef(Type type)
        {
            Requires.NotNull(type, "type");

            this.AssemblyName = type.Assembly.GetName();
            this.IsArray = type.IsArray;

            Type elementType = type.IsArray ? type.GetElementType() : type;
            this.MetadataToken = elementType.MetadataToken;
            this.GenericTypeParameterCount = elementType.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = elementType.GenericTypeArguments != null && elementType.GenericTypeArguments.Length > 0
                ? elementType.GenericTypeArguments.Select(t => new TypeRef(t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;
        }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken { get; private set; }

        public bool IsArray { get; private set; }

        public int GenericTypeParameterCount { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

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
                    Type type = Resolver.GetManifest(this.AssemblyName).ResolveType(this.MetadataToken);
                    if (this.GenericTypeArguments.Length > 0)
                    {
                        type = type.MakeGenericType(this.GenericTypeArguments.Select(a => a.ResolvedType).ToArray());
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
            return new TypeRef(assemblyName, metadataToken, isArray, genericTypeParameterCount, genericTypeArguments);
        }

        public static TypeRef Get(Type type)
        {
            if (type == null)
            {
                return null;
            }

            Requires.Argument(!type.IsGenericParameter, "type", "Generic type parameters are not allowed.");
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

            Debug.Assert(type.IsEquivalentTo(result.Resolve()));

            return result;
        }

        public TypeRef MakeGenericType(ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.Argument(!genericTypeArguments.IsDefault, "genericTypeArguments", "Not initialized.");
            Verify.Operation(this.IsGenericTypeDefinition, "This is not a generic type definition.");
            return new Reflection.TypeRef(this.AssemblyName, this.MetadataToken, this.IsArray, this.GenericTypeParameterCount, genericTypeArguments);
        }

        public override int GetHashCode()
        {
            return ByValueEquality.AssemblyName.GetHashCode(this.AssemblyName) + this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRef && this.Equals((TypeRef)obj);
        }

        public bool Equals(TypeRef other)
        {
            return ByValueEquality.AssemblyName.Equals(this.AssemblyName, other.AssemblyName)
                && this.MetadataToken == other.MetadataToken
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments);
        }

        public bool Equals(Type other)
        {
            return this.Equals(TypeRef.Get(other));
        }
    }
}
