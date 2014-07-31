namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public struct ConstructorRef : IEquatable<ConstructorRef>
    {
        public ConstructorRef(TypeRef declaringType, int metadataToken)
            : this()
        {
            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
        }

        public ConstructorRef(ConstructorInfo constructor)
            : this(TypeRef.Get(constructor.DeclaringType), constructor.MetadataToken) { }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        public bool Equals(ConstructorRef other)
        {
            return this.DeclaringType.Equals(other.DeclaringType)
                && this.MetadataToken == other.MetadataToken;
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructorRef && this.Equals((ConstructorRef)obj);
        }
    }
}
