namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

    public struct MethodRef : IEquatable<MethodRef>
    {
        public MethodRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> genericMethodArguments)
            : this()
        {
            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.GenericMethodArguments = genericMethodArguments;
        }

        public MethodRef(MethodInfo method)
            : this(new TypeRef(method.DeclaringType), method.MetadataToken, method.GetGenericArguments().Select(t => new TypeRef(t)).ToImmutableArray()) { }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public ImmutableArray<TypeRef> GenericMethodArguments { get; private set; }
        
        public bool IsEmpty
        {
            get { return this.DeclaringType.IsEmpty; }
        }

        public bool Equals(MethodRef other)
        {
            return this.DeclaringType.Equals(other.DeclaringType)
                && this.MetadataToken == other.MetadataToken
                && this.GenericMethodArguments.EqualsByValue(other.GenericMethodArguments);
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is MethodRef && this.Equals((MethodRef)obj);
        }
    }
}
