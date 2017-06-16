// Copyright (c) Microsoft. All rights reserved.

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
    public struct ConstructorRef : IEquatable<ConstructorRef>
    {
        public ConstructorRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> parameterTypes)
            : this()
        {
            Requires.NotNull(declaringType, nameof(declaringType));
            if (parameterTypes.IsDefault)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.ParameterTypes = parameterTypes;
        }

#if NET45
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

        public int MetadataToken { get; private set; }

        public ImmutableArray<TypeRef> ParameterTypes { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public static ConstructorRef Get(ConstructorInfo constructor, Resolver resolver)
        {
            return constructor != null
                ? new ConstructorRef(constructor, resolver)
                : default(ConstructorRef);
        }

        public bool Equals(ConstructorRef other)
        {
            if (this.IsEmpty && other.IsEmpty)
            {
                return true;
            }

            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            return !this.IsEmpty && !other.IsEmpty
                && EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
                && this.MetadataToken == other.MetadataToken;
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructorRef ctor && this.Equals(ctor);
        }
    }
}
