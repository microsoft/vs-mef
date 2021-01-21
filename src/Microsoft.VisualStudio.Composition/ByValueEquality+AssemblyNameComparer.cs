// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections.Generic;
    using System.Reflection;

    internal static partial class ByValueEquality
    {
        internal static IEqualityComparer<AssemblyName> AssemblyName
        {
            get { return AssemblyNameComparer.Default; }
        }

        internal static IEqualityComparer<AssemblyName> AssemblyNameNoFastCheck
        {
            get { return AssemblyNameComparer.NoFastCheck; }
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            internal static readonly AssemblyNameComparer Default = new AssemblyNameComparer();
            internal static readonly AssemblyNameComparer NoFastCheck = new AssemblyNameComparer(fastCheck: false);
            private bool fastCheck;

            internal AssemblyNameComparer(bool fastCheck = true)
            {
                this.fastCheck = fastCheck;
            }

            public bool Equals(AssemblyName? x, AssemblyName? y)
            {
                if (x is null && y is null)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                // If fast check is enabled, we can compare the code bases
                if (this.fastCheck && x.CodeBase == y.CodeBase)
                {
                    return true;
                }

                // There are some cases where two AssemblyNames who are otherwise equivalent
                // have a null PublicKey but a correct PublicKeyToken, and vice versa. We should
                // compare the PublicKeys first, but then fall back to GetPublicKeyToken(), which
                // will generate a public key token for the AssemblyName that has a public key and
                // return the public key token for the other AssemblyName.
                byte[]? xPublicKey = x.GetPublicKey();
                byte[]? yPublicKey = y.GetPublicKey();

                // Testing on FullName is horrifically slow.
                // So test directly on its components instead.
                if (xPublicKey != null && yPublicKey != null)
                {
                    return x.Name == y.Name
                        && Equals(x.Version, y.Version)
                        && string.Equals(x.CultureName, y.CultureName)
                        && ByValueEquality.Buffer.Equals(xPublicKey, yPublicKey);
                }

                return x.Name == y.Name
                    && Equals(x.Version, y.Version)
                    && string.Equals(x.CultureName, y.CultureName)
                    && ByValueEquality.Buffer.Equals(x.GetPublicKeyToken(), y.GetPublicKeyToken());
            }

            public int GetHashCode(AssemblyName obj) => obj.Name?.GetHashCode() ?? 0;
        }
    }
}
