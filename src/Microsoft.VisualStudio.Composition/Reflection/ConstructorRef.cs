namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [StructLayout(LayoutKind.Auto)] // Workaround multi-core JIT deadlock (DevDiv.1043199)
    public struct ConstructorRef : IEquatable<ConstructorRef>
    {
        public ConstructorRef(TypeRef declaringType, int metadataToken)
            : this()
        {
            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
        }

        public ConstructorRef(ConstructorInfo constructor, MyResolver resolver)
            : this(TypeRef.Get(constructor.DeclaringType, resolver), constructor.MetadataToken)
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal MyResolver Resolver => this.DeclaringType?.Resolver;

        public static ConstructorRef Get(ConstructorInfo constructor, MyResolver resolver)
        {
            return constructor != null
                ? new ConstructorRef(constructor, resolver)
                : default(ConstructorRef);
        }

        public bool Equals(ConstructorRef other)
        {
            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
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
