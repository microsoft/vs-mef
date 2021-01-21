// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
