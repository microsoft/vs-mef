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
    public struct MethodRef : IEquatable<MethodRef>
    {
        public MethodRef(TypeRef declaringType, int metadataToken, string name, ImmutableArray<TypeRef> parameterTypes, ImmutableArray<TypeRef> genericMethodArguments)
            : this()
        {
            Requires.NotNullOrEmpty(name, nameof(name));

            this.DeclaringType = declaringType;
            this.MetadataToken = metadataToken;
            this.ParameterTypes = parameterTypes;
            this.Name = name;
            this.GenericMethodArguments = genericMethodArguments;
        }

#if NET45
        [Obsolete]
        public MethodRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> genericMethodArguments)
            : this(
                  declaringType,
                  metadataToken,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveMethod(metadataToken).Name,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveMethod(metadataToken).GetParameterTypes(declaringType.Resolver),
                  genericMethodArguments)
        {
        }
#endif

        public MethodRef(MethodInfo method, Resolver resolver)
            : this(TypeRef.Get(method.DeclaringType, resolver), method.MetadataToken, method.Name, method.GetParameterTypes(resolver), method.GetGenericTypeArguments(resolver))
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public string Name { get; private set; }

        public ImmutableArray<TypeRef> ParameterTypes { get; private set; }

        public ImmutableArray<TypeRef> GenericMethodArguments { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public static MethodRef Get(MethodInfo method, Resolver resolver)
        {
            return method != null ? new MethodRef(method, resolver) : default(MethodRef);
        }

        public bool Equals(MethodRef other)
        {
            if (this.IsEmpty ^ other.IsEmpty)
            {
                return false;
            }

            if (this.IsEmpty)
            {
                return true;
            }

            // If we ever stop comparing metadata tokens,
            // we would need to compare the other properties that describe this member.
            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
                && this.MetadataToken == other.MetadataToken
                && this.GenericMethodArguments.EqualsByValue(other.GenericMethodArguments);
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is MethodRef method && this.Equals(method);
        }
    }
}
