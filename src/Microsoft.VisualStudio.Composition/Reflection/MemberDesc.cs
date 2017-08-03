// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;

    [Obsolete("Use " + nameof(MemberRef) + " instead.", error: true)]
    public abstract class MemberDesc
    {
        protected MemberDesc(string name, bool isStatic)
        {
            this.Name = name;
            this.IsStatic = isStatic;
        }

        public string Name { get; }

        public bool IsStatic { get; }
    }
}
