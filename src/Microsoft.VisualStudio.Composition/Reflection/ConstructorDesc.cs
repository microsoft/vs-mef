// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Obsolete("Use " + nameof(ConstructorRef) + " instead.", error: true)]
    public class ConstructorDesc : MemberDesc
    {
        public ConstructorDesc(ConstructorRef constructor, string name, bool isStatic)
            : base(name, isStatic)
        {
            this.Constructor = constructor;
        }

        public ConstructorRef Constructor { get; private set; }
    }
}
