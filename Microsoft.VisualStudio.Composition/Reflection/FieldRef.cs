namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public struct FieldRef : IEquatable<FieldRef>
    {
        public FieldRef(AssemblyName assemblyName, int metadataToken)
            : this()
        {
            Requires.NotNull(assemblyName, "assemblyName");

            this.AssemblyName = assemblyName;
            this.MetadataToken = metadataToken;
        }

        public FieldRef(FieldInfo field)
            : this(field.DeclaringType.GetTypeInfo().Assembly.GetName(), field.MetadataToken) { }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken { get; private set; }

        public bool IsEmpty
        {
            get { return this.AssemblyName == null; }
        }

        public bool Equals(FieldRef other)
        {
            return ByValueEquality.AssemblyName.Equals(this.AssemblyName, other.AssemblyName)
                && this.MetadataToken == other.MetadataToken;
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is FieldRef && this.Equals((FieldRef)obj);
        }
    }
}
