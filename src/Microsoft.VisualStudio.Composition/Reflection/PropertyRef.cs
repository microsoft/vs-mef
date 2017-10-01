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
        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? metadataToken;

        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? getMethodMetadataToken;

        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? setMethodMetadataToken;

        /// <summary>
        /// The <see cref="MemberInfo"/> that this value was instantiated with,
        /// or cached later when a metadata token was resolved.
        /// </summary>
        private PropertyInfo propertyInfo;

        public PropertyRef(TypeRef declaringType, int metadataToken, int? getMethodMetadataToken, int? setMethodMetadataToken, string name)
            : this()
        {
            this.DeclaringType = declaringType;
            this.metadataToken = metadataToken;
            this.getMethodMetadataToken = getMethodMetadataToken;
            this.setMethodMetadataToken = setMethodMetadataToken;
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
            this.metadataToken = propertyInfo.MetadataToken;
            this.propertyInfo = propertyInfo;
            this.Name = propertyInfo.Name;
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken => this.metadataToken ?? this.propertyInfo?.MetadataToken ?? 0;

        public PropertyInfo PropertyInfo => this.propertyInfo ?? (this.propertyInfo = this.Resolve());

        public int? GetMethodMetadataToken => this.getMethodMetadataToken ?? this.propertyInfo?.GetMethod?.MetadataToken;

        public int? SetMethodMetadataToken => this.setMethodMetadataToken ?? this.propertyInfo?.SetMethod?.MetadataToken;

        public string Name { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public bool Equals(PropertyRef other)
        {
            if (this.IsEmpty ^ other.IsEmpty)
            {
                return false;
            }

            if (this.IsEmpty)
            {
                return true;
            }

            if (this.propertyInfo != null && other.propertyInfo != null)
            {
                if (this.propertyInfo == other.propertyInfo)
                {
                    return true;
                }
            }

            if (this.Name != other.Name)
            {
                return false;
            }

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType);
        }

        public override int GetHashCode()
        {
            return this.DeclaringType.GetHashCode() + this.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyRef prop && this.Equals(prop);
        }
    }
}
