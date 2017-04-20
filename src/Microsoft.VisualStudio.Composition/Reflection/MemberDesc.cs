// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

    public abstract class MemberDesc
    {
        protected MemberDesc(string name, bool isStatic)
        {
            this.Name = name;
            this.IsStatic = isStatic;
        }

        public string Name { get; private set; }

        public bool IsStatic { get; private set; }
    }
}
