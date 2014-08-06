namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public struct ParameterRef : IEquatable<ParameterRef>
    {
        /// <summary>
        /// A 1-based index describing which parameter in the method this references.
        /// </summary>
        /// <remarks>
        /// This is a 1-based index so that 0 can be recognized as an empty value.
        /// </remarks>
        private readonly int parameterIndex;

        public ParameterRef(AssemblyName assemblyName, int methodMetadataToken, int parameterIndex)
            : this()
        {
            this.AssemblyName = assemblyName;
            this.MethodMetadataToken = methodMetadataToken;
            this.parameterIndex = parameterIndex + 1;
        }

        public ParameterRef(ConstructorRef ctor, int parameterIndex)
            : this(ctor.DeclaringType.AssemblyName, ctor.MetadataToken, parameterIndex)
        {
        }

        public ParameterRef(MethodRef method, int parameterIndex)
            : this(method.DeclaringType.AssemblyName, method.MetadataToken, parameterIndex)
        {
        }

        public ParameterRef(ParameterInfo parameter)
            : this(parameter.Member.DeclaringType.Assembly.GetName(), parameter.Member.MetadataToken, parameter.Position) { }

        public AssemblyName AssemblyName { get; private set; }

        public int MethodMetadataToken { get; private set; }

        public int ParameterIndex
        {
            get { return this.parameterIndex - 1; }
        }

        public bool IsEmpty
        {
            get { return this.parameterIndex == 0; }
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
