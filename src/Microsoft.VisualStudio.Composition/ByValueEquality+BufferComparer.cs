// Copyright (c) Microsoft. All rights reserved.

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

            public bool Equals(byte[] x, byte[] y)
            {
                if (x == y)
                {
                    return true;
                }

                if (x == null ^ y == null)
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
