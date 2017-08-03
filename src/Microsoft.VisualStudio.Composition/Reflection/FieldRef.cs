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
    public struct FieldRef : IEquatable<FieldRef>
    {
        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? metadataToken;

        /// <summary>
        /// The <see cref="MemberInfo"/> that this value was instantiated with,
        /// or cached later when a metadata token was resolved.
        /// </summary>
        private FieldInfo fieldInfo;

        public FieldRef(TypeRef declaringType, int metadataToken, string name)
            : this()
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            Requires.NotNullOrEmpty(name, nameof(name));

            this.DeclaringType = declaringType;
            this.metadataToken = metadataToken;
            this.Name = name;
        }

#if NET45
        [Obsolete]
        public FieldRef(TypeRef declaringType, int metadataToken)
            : this(
                  declaringType,
                  metadataToken,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveField(metadataToken).Name)
        {
        }
#endif

        public FieldRef(FieldInfo field, Resolver resolver)
            : this(TypeRef.Get(field.DeclaringType, resolver), field.MetadataToken, field.Name)
        {
            this.fieldInfo = field;
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken => this.metadataToken ?? this.fieldInfo.MetadataToken;

        public FieldInfo FieldInfo => this.fieldInfo ?? (this.fieldInfo = this.Resolve());

        public string Name { get; private set; }

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
            if (this.IsEmpty ^ other.IsEmpty)
            {
                return false;
            }

            if (this.IsEmpty)
            {
                return true;
            }

            if (this.fieldInfo != null && other.fieldInfo != null)
            {
                if (this.fieldInfo == other.fieldInfo)
                {
                    return true;
                }
            }

            if (this.metadataToken.HasValue && other.metadataToken.HasValue)
            {
                if (this.metadataToken.Value != other.metadataToken.Value)
                {
                    return false;
                }
            }
            else
            {
                if (this.Name != other.Name)
                {
                    return false;
                }
            }

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType);
        }

        public override int GetHashCode()
        {
            return this.DeclaringType.GetHashCode() + this.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is FieldRef field && this.Equals(field);
        }
    }
}
