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

        public ParameterRef(MethodRef method, int parameterIndex)
            : this()
        {
            this.Method = method;
            this.parameterIndex = parameterIndex;
        }

        public ParameterRef(ConstructorRef ctor, int parameterIndex)
            : this()
        {
            this.Constructor = ctor;
            this.parameterIndex = parameterIndex;
        }

        public MethodRef Method { get; private set; }

        public ConstructorRef Constructor { get; private set; }

        public int ParameterIndex
        {
            get { return this.parameterIndex; }
        }

        public AssemblyName AssemblyName
        {
            get { return this.DeclaringType.AssemblyName; }
        }

        public bool IsEmpty
        {
            get { return this.Method.IsEmpty && this.Constructor.IsEmpty; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        internal TypeRef DeclaringType => this.Constructor.DeclaringType ?? this.Method.DeclaringType;

        public static ParameterRef Get(ParameterInfo parameter, Resolver resolver)
        {
            if (parameter != null)
            {
                if (parameter.Member is ConstructorInfo ctor)
                {
                    return new ParameterRef(new ConstructorRef(ctor, resolver), parameter.Position);
                }
                else if (parameter.Member is MethodInfo methodInfo)
                {
                    return new ParameterRef(new MethodRef(methodInfo, resolver), parameter.Position);
                }
                else
                {
                    throw new NotSupportedException("Unsupported member type: " + parameter.Member.GetType().Name);
                }
            }

            return default(ParameterRef);
        }

        public bool Equals(ParameterRef other)
        {
            return this.Constructor.Equals(other.Constructor)
                && this.Method.Equals(other.Method)
                && this.ParameterIndex == other.ParameterIndex;
        }

        public override int GetHashCode()
        {
            return unchecked(this.Method.MetadataToken + this.Constructor.MetadataToken + this.parameterIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is ParameterRef && this.Equals((ParameterRef)obj);
        }
    }
}
