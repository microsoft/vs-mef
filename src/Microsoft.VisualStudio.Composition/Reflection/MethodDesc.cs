// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class MethodDesc : MemberDesc
    {
        public MethodDesc(MethodRef method, string name, bool isStatic, TypeRef returnType, ImmutableArray<TypeRef> parameters)
            : base(name, isStatic)
        {
            this.Method = method;
            this.ReturnType = returnType;
            this.Parameters = parameters;
        }

        public MethodRef Method { get; private set; }

        public TypeRef ReturnType { get; private set; }

        public ImmutableArray<TypeRef> Parameters { get; private set; }
    }
}
