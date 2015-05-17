namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    internal class UniquePropertyNameComparer : IEqualityComparer<PropertyInfo>
    {
        internal readonly static IEqualityComparer<PropertyInfo> Default = new UniquePropertyNameComparer();

        private UniquePropertyNameComparer()
        {
        }

        public bool Equals(PropertyInfo x, PropertyInfo y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(PropertyInfo obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
