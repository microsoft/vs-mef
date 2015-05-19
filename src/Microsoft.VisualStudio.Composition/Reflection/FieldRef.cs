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
    public struct FieldRef : IEquatable<FieldRef>
    {
        public FieldRef(TypeRef declaringType, int metadataToken)
            : this()
        {
            Requires.NotNull(declaringType, nameof(declaringType));

            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
        }

        public FieldRef(FieldInfo field, Resolver resolver)
            : this(TypeRef.Get(field.DeclaringType, resolver), field.MetadataToken)
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public AssemblyName AssemblyName
        {
            get { return this.IsEmpty ? null : this.DeclaringType.AssemblyName; }
        }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public bool Equals(FieldRef other)
        {
            return ByValueEquality.AssemblyName.Equals(this.AssemblyName, other.AssemblyName)
                && this.MetadataToken == other.MetadataToken;
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is FieldRef && this.Equals((FieldRef)obj);
        }
    }
}
