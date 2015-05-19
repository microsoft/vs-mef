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
    public struct PropertyRef : IEquatable<PropertyRef>
    {
        public PropertyRef(TypeRef declaringType, int metadataToken, int? getMethodMetadataToken, int? setMethodMetadataToken)
            : this()
        {
            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.GetMethodMetadataToken = getMethodMetadataToken;
            this.SetMethodMetadataToken = setMethodMetadataToken;
        }

        public PropertyRef(PropertyInfo propertyInfo, MyResolver resolver)
            : this()
        {
            this.DeclaringType = TypeRef.Get(propertyInfo.DeclaringType, resolver);
            this.MetadataToken = propertyInfo.MetadataToken;
            this.GetMethodMetadataToken = propertyInfo.GetMethod != null ? (int?)propertyInfo.GetMethod.MetadataToken : null;
            this.SetMethodMetadataToken = propertyInfo.SetMethod != null ? (int?)propertyInfo.SetMethod.MetadataToken : null;
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public int? GetMethodMetadataToken { get; private set; }

        public int? SetMethodMetadataToken { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal MyResolver Resolver => this.DeclaringType?.Resolver;

        public bool Equals(PropertyRef other)
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
            return obj is PropertyRef && this.Equals((PropertyRef)obj);
        }
    }
}
