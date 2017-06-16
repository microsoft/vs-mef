// Copyright (c) Microsoft. All rights reserved.

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
        public PropertyRef(TypeRef declaringType, int metadataToken, int? getMethodMetadataToken, int? setMethodMetadataToken, string name)
            : this()
        {
            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.GetMethodMetadataToken = getMethodMetadataToken;
            this.SetMethodMetadataToken = setMethodMetadataToken;
            this.Name = name;
        }

#if NET45
        [Obsolete]
        public PropertyRef(TypeRef declaringType, int metadataToken, int? getMethodMetadataToken, int? setMethodMetadataToken)
            : this(
                  declaringType,
                  metadataToken,
                  getMethodMetadataToken,
                  setMethodMetadataToken,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveMember(metadataToken).Name)
        {
        }
#endif

        public PropertyRef(PropertyInfo propertyInfo, Resolver resolver)
            : this()
        {
            this.DeclaringType = TypeRef.Get(propertyInfo.DeclaringType, resolver);
            this.MetadataToken = propertyInfo.MetadataToken;
            this.GetMethodMetadataToken = propertyInfo.GetMethod != null ? (int?)propertyInfo.GetMethod.MetadataToken : null;
            this.SetMethodMetadataToken = propertyInfo.SetMethod != null ? (int?)propertyInfo.SetMethod.MetadataToken : null;
            this.Name = propertyInfo.Name;
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public int? GetMethodMetadataToken { get; private set; }

        public int? SetMethodMetadataToken { get; private set; }

        public string Name { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public bool Equals(PropertyRef other)
        {
            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
                && this.MetadataToken == other.MetadataToken;
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyRef prop && this.Equals(prop);
        }
    }
}
