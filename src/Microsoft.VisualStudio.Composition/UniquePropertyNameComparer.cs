// Copyright (c) Microsoft. All rights reserved.

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
        internal static readonly IEqualityComparer<PropertyInfo> Default = new UniquePropertyNameComparer();

        private UniquePropertyNameComparer()
        {
        }

        public bool Equals(PropertyInfo? x, PropertyInfo? y)
        {
            if (x == y)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Name == y.Name;
        }

        public int GetHashCode(PropertyInfo obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
