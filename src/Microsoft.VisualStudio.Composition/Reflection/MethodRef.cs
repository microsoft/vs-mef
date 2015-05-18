namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [StructLayout(LayoutKind.Auto)] // Workaround multi-core JIT deadlock (DevDiv.1043199)
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
            : this(TypeRef.Get(method.DeclaringType), method.MetadataToken, method.GetGenericArguments().Select(TypeRef.Get).ToImmutableArray())
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public ImmutableArray<TypeRef> GenericMethodArguments { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        public static MethodRef Get(MethodInfo method)
        {
            return method != null ? new MethodRef(method) : default(MethodRef);
        }

        public bool Equals(MethodRef other)
        {
            if (this.IsEmpty ^ other.IsEmpty)
            {
                return false;
            }

            if (this.IsEmpty)
            {
                return true;
            }

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
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
