namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public struct ParameterRef : IEquatable<ParameterRef>
    {
        /// <summary>
        /// A 0-based index describing which parameter in the method this references.
        /// </summary>
        private readonly int parameterIndex;

        public ParameterRef(TypeRef declaringType, int methodMetadataToken, int parameterIndex)
            : this()
        {
            Requires.NotNull(declaringType, "declaringType");

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

        public ParameterRef(ParameterInfo parameter)
            : this(TypeRef.Get(parameter.Member.DeclaringType), parameter.Member.MetadataToken, parameter.Position) { }

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

        public static ParameterRef Get(ParameterInfo parameter)
        {
            return parameter != null ? new ParameterRef(parameter) : default(ParameterRef);
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
