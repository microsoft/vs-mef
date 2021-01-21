// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class MemberRef : IEquatable<MemberRef>
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
        private MemberInfo? cachedMemberInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberRef"/> class.
        /// </summary>
        protected MemberRef(TypeRef declaringType, int metadataToken, bool isStatic)
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            this.DeclaringType = declaringType;
            this.metadataToken = metadataToken;
            this.IsStatic = isStatic;
        }

        protected MemberRef(TypeRef declaringType, MemberInfo memberInfo)
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            Requires.NotNull(memberInfo, nameof(memberInfo));

            this.DeclaringType = declaringType;
            this.cachedMemberInfo = memberInfo;
            this.IsStatic = memberInfo.IsStatic();
        }

        protected MemberRef(MemberInfo memberInfo, Resolver resolver)
            : this(
                 TypeRef.Get(Requires.NotNull(memberInfo, nameof(memberInfo)).DeclaringType ?? throw new ArgumentException("DeclaringType is null", nameof(memberInfo)), resolver),
                 memberInfo)
        {
        }

        public TypeRef DeclaringType { get; }

        public AssemblyName AssemblyName => this.DeclaringType.AssemblyName;

        public abstract string Name { get; }

        public bool IsStatic { get; }

        public int MetadataToken => this.metadataToken ?? this.cachedMemberInfo?.GetMetadataTokenSafe() ?? 0;

        public MemberInfo MemberInfo => this.cachedMemberInfo ?? (this.cachedMemberInfo = this.Resolve());

        internal MemberInfo? MemberInfoNoResolve => this.cachedMemberInfo;

        internal Resolver Resolver => this.DeclaringType.Resolver;

        [return: NotNullIfNotNull("member")]
        public static MemberRef? Get(MemberInfo member, Resolver resolver)
        {
            if (member == null)
            {
                return null;
            }

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return new FieldRef((FieldInfo)member, resolver);
                case MemberTypes.Constructor:
                case MemberTypes.Method:
                    return new MethodRef((MethodInfo)member, resolver);
                case MemberTypes.Property:
                    return new PropertyRef((PropertyInfo)member, resolver);
                default:
                    throw new NotSupportedException();
            }
        }

        public virtual bool Equals(MemberRef? other)
        {
            if (other == null || !this.GetType().IsEquivalentTo(other.GetType()))
            {
                return false;
            }

            if (this.cachedMemberInfo != null && other.cachedMemberInfo != null)
            {
                if (this.cachedMemberInfo == other.cachedMemberInfo)
                {
                    return true;
                }
            }

            if (this.metadataToken.HasValue && other.metadataToken.HasValue && this.DeclaringType.AssemblyId.Equals(other.DeclaringType.AssemblyId))
            {
                if (this.metadataToken.Value != other.metadataToken.Value)
                {
                    return false;
                }
            }
            else
            {
                if (!this.EqualsByTypeLocalMetadata(other))
                {
                    return false;
                }
            }

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is equivalent to another one,
        /// based only on metadata that describes this member, assuming the declaring types are equal.
        /// </summary>
        /// <param name="other">The instance to compare with. This may be assumed to always be an instance of the same type.</param>
        /// <returns><c>true</c> if the local metadata on the member are equal; <c>false</c> otherwise.</returns>
        protected abstract bool EqualsByTypeLocalMetadata(MemberRef other);

        protected abstract MemberInfo Resolve();

        internal abstract void GetInputAssemblies(ISet<AssemblyName> assemblies);

        public override int GetHashCode()
        {
            // Derived types must override this.
            throw new NotImplementedException();
        }

        public override bool Equals(object? obj)
        {
            return obj is MemberRef && this.Equals((MemberRef)obj);
        }
    }
}
