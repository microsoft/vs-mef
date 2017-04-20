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
    public struct ParameterRef : IEquatable<ParameterRef>
    {
        /// <summary>
        /// A 0-based index describing which parameter in the method this references.
        /// </summary>
        private readonly int parameterIndex;

        public ParameterRef(TypeRef declaringType, int methodMetadataToken, int parameterIndex)
            : this()
        {
            Requires.NotNull(declaringType, nameof(declaringType));

            this.DeclaringType = declaringType;
            this.MethodMetadataToken = methodMetadataToken;
            this.parameterIndex = parameterIndex;
        }

        public ParameterRef(ConstructorRef ctor, int parameterIndex)
            : this(ctor.DeclaringType, ctor.MetadataToken, parameterIndex)
        {
        }

        public ParameterRef(MethodRef method, int parameterIndex)
            : this(method.DeclaringType, method.MetadataToken, parameterIndex)
        {
        }

        public ParameterRef(ParameterInfo parameter, Resolver resolver)
            : this(TypeRef.Get(parameter.Member.DeclaringType, resolver), parameter.Member.MetadataToken, parameter.Position)
        {
        }

        public TypeRef DeclaringType { get; private set; }

        public int MethodMetadataToken { get; private set; }

        public int ParameterIndex
        {
            get { return this.parameterIndex; }
        }

        public AssemblyName AssemblyName
        {
            get { return this.IsEmpty ? null : this.DeclaringType.AssemblyName; }
        }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public static ParameterRef Get(ParameterInfo parameter, Resolver resolver)
        {
            return parameter != null ? new ParameterRef(parameter, resolver) : default(ParameterRef);
        }

        public bool Equals(ParameterRef other)
        {
            return this.MethodMetadataToken.Equals(other.MethodMetadataToken)
                && this.ParameterIndex == other.ParameterIndex;
        }

        public override int GetHashCode()
        {
            return unchecked(this.MethodMetadataToken + this.parameterIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is ParameterRef && this.Equals((ParameterRef)obj);
        }
    }
}
