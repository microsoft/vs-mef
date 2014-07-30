namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class TypeDesc
    {
        public TypeDesc(TypeRef type, string fullName)
        {
            this.Type = type;
            this.FullName = fullName;
        }

        public TypeRef Type { get; private set; }

        public string FullName { get; private set; }

        public static TypeDesc Get(Type type)
        {
            Requires.NotNull(type, "type");
            return new TypeDesc(new TypeRef(type), type.FullName);
        }
    }
}
