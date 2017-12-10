// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Obsolete("Use " + nameof(FieldRef) + " instead.", error: true)]
    public class FieldDesc : MemberDesc
    {
        public FieldDesc(FieldRef fieldRef, TypeDesc fieldType, string name, bool isStatic)
            : base(name, isStatic)
        {
            this.Field = fieldRef;
            this.FieldType = fieldType;
        }

        public FieldRef Field { get; private set; }

        public TypeDesc FieldType { get; private set; }
    }
}
