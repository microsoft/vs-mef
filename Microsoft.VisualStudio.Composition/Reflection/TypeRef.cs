namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public struct TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
        public TypeRef(AssemblyName assemblyName, int metadataToken, ImmutableArray<TypeRef> genericTypeArguments)
            : this()
        {
            Requires.NotNull(assemblyName, "assemblyName");

            this.AssemblyName = assemblyName;
            this.MetadataToken = metadataToken;
            this.GenericTypeArguments = genericTypeArguments;
        }

        public TypeRef(Type type)
            : this()
        {
            this.AssemblyName = type.Assembly.GetName();
            this.MetadataToken = type.MetadataToken;
            this.GenericTypeArguments = type.GenericTypeArguments != null && type.GenericTypeArguments.Length > 0
                ? type.GenericTypeArguments.Select(t => new TypeRef(t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;
        }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        public bool IsEmpty
        {
            get { return this.AssemblyName == null; }
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRef && this.Equals((TypeRef)obj);
        }

        public bool Equals(TypeRef other)
        {
            return this.AssemblyName == other.AssemblyName
                && this.MetadataToken == other.MetadataToken
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments);
        }

        public bool Equals(Type other)
        {
            return this.Equals(new TypeRef(other));
        }
    }
}
