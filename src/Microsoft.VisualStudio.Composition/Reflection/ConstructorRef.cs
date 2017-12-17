// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    [StructLayout(LayoutKind.Auto)] // Workaround multi-core JIT deadlock (DevDiv.1043199)
    public struct ConstructorRef : IEquatable<ConstructorRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string DebuggerDisplay => this.IsEmpty ? "(empty)" : $"{this.DeclaringType.FullName}.{ConstructorInfo.ConstructorName}({string.Join(", ", this.ParameterTypes.Select(p => p.FullName))})";

        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? metadataToken;

        /// <summary>
        /// The <see cref="MemberInfo"/> that this value was instantiated with,
        /// or cached later when a metadata token was resolved.
        /// </summary>
        private ConstructorInfo constructorInfo;

        public ConstructorRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> parameterTypes)
            : this()
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            if (parameterTypes.IsDefault)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            this.DeclaringType = declaringType;
            this.metadataToken = metadataToken;
            this.ParameterTypes = parameterTypes;
        }

#if DESKTOP
        [Obsolete]
        public ConstructorRef(TypeRef declaringType, int metadataToken)
            : this(
                  declaringType,
                  metadataToken,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveMethod(metadataToken).GetParameterTypes(declaringType.Resolver))
        {
        }
#endif

        public ConstructorRef(ConstructorInfo constructor, Resolver resolver)
            : this(TypeRef.Get(constructor.DeclaringType, resolver), constructor.MetadataToken, constructor.GetParameterTypes(resolver))
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken => this.metadataToken ?? this.constructorInfo.MetadataToken;

        public ConstructorInfo ConstructorInfo => this.constructorInfo ?? (this.constructorInfo = this.Resolve());

        public ImmutableArray<TypeRef> ParameterTypes { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        internal ConstructorInfo ConstructorInfoNoResolve => this.constructorInfo;

        public static ConstructorRef Get(ConstructorInfo constructor, Resolver resolver)
        {
            return constructor != null
                ? new ConstructorRef(constructor, resolver)
                : default(ConstructorRef);
        }

        public bool Equals(ConstructorRef other)
        {
            if (this.IsEmpty ^ other.IsEmpty)
            {
                return false;
            }

            if (this.IsEmpty)
            {
                return true;
            }

            if (this.constructorInfo != null && other.constructorInfo != null)
            {
                if (this.constructorInfo == other.constructorInfo)
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
                if (!this.ParameterTypes.EqualsByValue(other.ParameterTypes))
                {
                    return false;
                }
            }

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType);
        }

        public override int GetHashCode()
        {
            return this.DeclaringType.GetHashCode() + this.ParameterTypes.Length;
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructorRef ctor && this.Equals(ctor);
        }
    }
}
