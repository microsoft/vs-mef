namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Reflection;
    using Xunit;

    public class ByValueEqualityTests
    {
        [Fact]
        public void AssemblyNameComparer_ComparesPublicKeyTokenWhenPublicKeyIsNull()
        {
            // Since "this" assembly is signed, we can grab the public key from it so that
            // we can create a valid assembly name with a valid public key.
            byte[] publicKey = Assembly.GetExecutingAssembly().GetName().GetPublicKey();
            byte[] publicKeyToken = Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken();
            Assert.NotNull(publicKey);
            Assert.NotNull(publicKeyToken);

            // a1 has a public key set.
            AssemblyName a1 = new AssemblyName();
            a1.Name = "AssemblyA";
            a1.Version = new Version(1, 0);
            a1.SetPublicKey(publicKey);

            // a2 has a public key token set, but not a public key.
            AssemblyName a2 = new AssemblyName();
            a2.Name = "AssemblyA";
            a2.Version = new Version(1, 0);
            a2.SetPublicKeyToken(publicKeyToken);

            // If the public key is not null for a2, then the equality comparison would just use the public keys for comparison,
            // rendering this test pointless.
            Assert.Null(a2.GetPublicKey());
            Assert.Equal(a1, a2, ByValueEquality.AssemblyName);
        }
    }
}
