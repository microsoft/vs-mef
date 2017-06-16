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

#if NET45
        [Obsolete]
        public ParameterRef(TypeRef declaringType, int methodMetadataToken, int parameterIndex)
        {
            var methodBase = declaringType.Resolve().Assembly.ManifestModule.ResolveMethod(methodMetadataToken);
            if (methodBase is ConstructorInfo ctor)
            {
                this.Constructor = new ConstructorRef(ctor, declaringType.Resolver);
                this.Method = default(MethodRef);
            }
            else
            {
                this.Method = new MethodRef((MethodInfo)methodBase, declaringType.Resolver);
                this.Constructor = default(ConstructorRef);
            }

            this.parameterIndex = parameterIndex;
        }

        [Obsolete]
        public ParameterRef(ParameterInfo parameter, Resolver resolver)
        {
            var memberInfo = parameter.Member;
            var declaringType = memberInfo.DeclaringType;
            if (memberInfo is ConstructorInfo ctor)
            {
                this.Constructor = new ConstructorRef(ctor, resolver);
                this.Method = default(MethodRef);
            }
            else
            {
                this.Method = new MethodRef((MethodInfo)memberInfo, resolver);
                this.Constructor = default(ConstructorRef);
            }

            this.parameterIndex = parameter.Position;
        }
#endif

        public ParameterRef(ConstructorRef ctor, int parameterIndex)
            : this()
        {
            this.Constructor = ctor;
            this.parameterIndex = parameterIndex;
        }

        public MethodRef Method { get; private set; }

        public ConstructorRef Constructor { get; private set; }

        public TypeRef DeclaringType => this.Constructor.DeclaringType ?? this.Method.DeclaringType;

        public int MethodMetadataToken => this.Constructor.IsEmpty ? this.Method.MetadataToken : this.Constructor.MetadataToken;

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
            return obj is ParameterRef parameter && this.Equals(parameter);
        }
    }
}
