namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

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
