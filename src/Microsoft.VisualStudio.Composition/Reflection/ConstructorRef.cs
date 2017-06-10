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
        private Type[] resolvedParameterTypes;

        public ConstructorRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> parameterTypes)
            : this()
        {
            if (parameterTypes.IsDefault)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.ParameterTypes = parameterTypes;
        }

        public ConstructorRef(ConstructorInfo constructor, Resolver resolver)
            : this(TypeRef.Get(constructor.DeclaringType, resolver), constructor.MetadataToken, constructor.GetParameterTypes(resolver))
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public ImmutableArray<TypeRef> ParameterTypes { get; private set; }

        public Type[] ResolvedParameterTypes
        {
            get
            {
                if (this.resolvedParameterTypes == null)
                {
                    this.resolvedParameterTypes = this.ParameterTypes.Select(a => a.Resolve()).ToArray();
                }

                return this.resolvedParameterTypes;
            }
        }

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

            return !this.IsEmpty && !other.IsEmpty
                && EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
                && this.MetadataToken == other.MetadataToken
                && this.ParameterTypes.EqualsByValue(other.ParameterTypes);
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is ConstructorRef && this.Equals((ConstructorRef)obj);
        }
    }
}
