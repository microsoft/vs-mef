// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;

    [Obsolete("Use " + nameof(PropertyRef) + " instead.", error: true)]
    public class PropertyDesc : MemberDesc
    {
        public PropertyDesc(PropertyDesc property, TypeDesc propertyType, string name, bool isStatic)
            : base(name, isStatic)
        {
            this.Property = property;
            this.PropertyType = propertyType;
        }

        public PropertyDesc Property { get; private set; }

        public TypeDesc PropertyType { get; private set; }
    }
}
