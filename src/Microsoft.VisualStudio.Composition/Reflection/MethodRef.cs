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
        private Type[] resolvedParameterTypes;

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

        public MethodRef(MethodInfo method, Resolver resolver)
            : this(TypeRef.Get(method.DeclaringType, resolver), method.MetadataToken, method.Name, method.GetParameterTypes(resolver), method.GetGenericTypeArguments(resolver))
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken { get; private set; }

        public string Name { get; private set; }

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

            return EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
                && this.MetadataToken == other.MetadataToken
                && this.Name == other.Name
                && this.GenericMethodArguments.EqualsByValue(other.GenericMethodArguments);
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is MethodRef && this.Equals((MethodRef)obj);
        }
    }
}
