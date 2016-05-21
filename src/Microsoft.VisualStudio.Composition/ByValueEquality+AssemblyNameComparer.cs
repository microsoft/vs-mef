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

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            internal static readonly AssemblyNameComparer Default = new AssemblyNameComparer();

            internal AssemblyNameComparer()
            {
            }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                if (x == null ^ y == null)
                {
                    return false;
                }

                if (x == null)
                {
                    return true;
                }

                // fast path
                if (x.CodeBase == y.CodeBase)
                {
                    return true;
                }

                // There are some cases where two AssemblyNames who are otherwise equivalent
                // have a null PublicKey but a correct PublicKeyToken, and vice versa. We should
                // compare the PublicKeys first, but then fall back to GetPublicKeyToken(), which
                // will generate a public key token for the AssemblyName that has a public key and
                // return the public key token for the other AssemblyName.
                byte[] xPublicKey = x.GetPublicKey();
                byte[] yPublicKey = y.GetPublicKey();

                // Testing on FullName is horrifically slow.
                // So test directly on its components instead.
                if (xPublicKey != null && yPublicKey != null)
                {
                    return x.Name == y.Name
                        && x.Version.Equals(y.Version)
                        && x.CultureName.Equals(y.CultureName)
                        && ByValueEquality.Buffer.Equals(xPublicKey, yPublicKey);
                }

                return x.Name == y.Name
                    && x.Version.Equals(y.Version)
                    && x.CultureName.Equals(y.CultureName)
                    && ByValueEquality.Buffer.Equals(x.GetPublicKeyToken(), y.GetPublicKeyToken());
            }

            public int GetHashCode(AssemblyName obj)
            {
                return obj.Name.GetHashCode();
            }
        }
    }
}
