// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;

    internal static partial class ByValueEquality
    {
        internal static IEqualityComparer<byte[]> Buffer
        {
            get { return BufferComparer.Default; }
        }

        private class BufferComparer : IEqualityComparer<byte[]>
        {
            internal static readonly BufferComparer Default = new BufferComparer();

            private BufferComparer()
            {
            }

            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == y)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                if (x.Length != y.Length)
                {
                    return false;
                }

                for (int i = 0; i < x.Length; i++)
                {
                    if (x[i] != y[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(byte[] obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
