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
    public class ConstructorRef : MethodRef, IEquatable<ConstructorRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal override string DebuggerDisplay => $"{this.DeclaringType.FullName}.{ConstructorInfo.ConstructorName}({string.Join(", ", this.ParameterTypes.Select(p => p.FullName))})";

        public ConstructorRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> parameterTypes)
            : base(declaringType, metadataToken, ConstructorInfo.ConstructorName, parameterTypes, ImmutableArray<TypeRef>.Empty)
        {
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

        public ConstructorInfo ConstructorInfo => (ConstructorInfo)this.MemberInfo;

        internal ConstructorInfo ConstructorInfoNoResolve => (ConstructorInfo)this.MemberInfoNoResolve;

        public static ConstructorRef Get(ConstructorInfo constructor, Resolver resolver)
        {
            return constructor != null
                ? new ConstructorRef(constructor, resolver)
                : default(ConstructorRef);
        }

        public override int GetHashCode() => base.GetHashCode() + this.ParameterTypes.Length;

        public bool Equals(ConstructorRef other) => this.Equals((MemberRef)other);
    }
}
