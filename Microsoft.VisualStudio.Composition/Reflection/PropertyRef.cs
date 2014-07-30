namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public struct PropertyRef : IEquatable<PropertyRef>
    {
        public PropertyRef(TypeRef declaringType, int metadataToken, TypeRef propertyType, int? getMethodMetadataToken, int? setMethodMetadataToken)
            : this()
        {
            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.GetMethodMetadataToken = getMethodMetadataToken;
            this.SetMethodMetadataToken = setMethodMetadataToken;
        }

        public PropertyRef(PropertyInfo propertyInfo)
            : this()
        {
            this.DeclaringType = new TypeRef(propertyInfo.DeclaringType);
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
            get { return this.DeclaringType == null || this.DeclaringType.IsEmpty; }
        }

        public bool Equals(PropertyRef other)
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
            return obj is PropertyRef && this.Equals((PropertyRef)obj);
        }
    }
}
