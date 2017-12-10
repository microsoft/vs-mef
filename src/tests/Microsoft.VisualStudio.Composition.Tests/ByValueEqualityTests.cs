// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using Xunit;

    public class ByValueEqualityTests
    {
        [Fact]
        public void AssemblyNameComparer_EqualityIsCommutative()
        {
            byte[] publicKey = GetPublicKeyFromExecutingAssembly();

            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\other\path\AssemblyA.dll", publicKey: publicKey);

            Assert.Equal(a1, a2, ByValueEquality.AssemblyName);
            Assert.Equal(a2, a1, ByValueEquality.AssemblyName);
        }

        [Fact]
        public void AssemblyNameComparer_ComparesPublicKeyTokenWhenPublicKeyIsNull()
        {
            // Since "this" assembly is signed, we can grab the public key from it so that
            // we can create a valid assembly name with a valid public key.
            byte[] publicKey = GetPublicKeyFromExecutingAssembly();
            byte[] publicKeyToken = GetPublicKeyTokenFromExecutingAssembly();

            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\other\path\AssemblyA.dll", publicKeyToken: publicKeyToken);

            // If the public key is not null for a2, then the equality comparison would just use the public keys for comparison,
            // rendering this test pointless.
            Assert.Null(a2.GetPublicKey());
            Assert.Equal(a1, a2, ByValueEquality.AssemblyName);
        }

        [Fact]
        public void AssemblyNameComparer_ComparesAssemblyNamesWithoutPublicKeysCorrectly()
        {
            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll");
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\other\path\AssemblyA.dll");

            Assert.Equal(a1, a2, ByValueEquality.AssemblyName);
        }

        [Fact]
        public void AssemblyNameComparer_ComparesAssemblyNamesWithOnlyPublicKeyTokens()
        {
            byte[] publicKeyToken = GetPublicKeyTokenFromExecutingAssembly();
            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKeyToken: publicKeyToken);
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\other\path\AssemblyA.dll", publicKeyToken: publicKeyToken);

            Assert.Equal(a1, a2, ByValueEquality.AssemblyName);
        }

        [Fact]
        public void AssemblyNameComparer_ComparesAssemblyNamesWithOnlyPublicKeys()
        {
            byte[] publicKey = GetPublicKeyFromExecutingAssembly();
            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\other\path\AssemblyA.dll", publicKey: publicKey);

            Assert.Equal(a1, a2, ByValueEquality.AssemblyName);
        }

        [Fact]
        public void AssemblyNameComparer_ReturnsFalseWhenPublicKeysDontMatch()
        {
            byte[] publicKey = GetPublicKeyFromExecutingAssembly();

            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\other\path\AssemblyA.dll");

            // a2 does not have a public key set, so the assemblies are different
            Assert.NotEqual(a1, a2, ByValueEquality.AssemblyName);
        }

        [Fact]
        public void AssemblyNameComparerNoFastCheck_SkipsCodeBaseCheck()
        {
            byte[] publicKey = GetPublicKeyFromExecutingAssembly();
            AssemblyName a1 = CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);
            AssemblyName a2 = CreateAssemblyName("AssemblyA", new Version(2, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);

            // a1 is version 1.0.0.0, a2 is version 2.0.0.0, so the assemblies are different
            Assert.NotEqual(a1, a2, ByValueEquality.AssemblyNameNoFastCheck);
        }

        private static byte[] GetPublicKeyFromExecutingAssembly()
        {
            byte[] publicKey = typeof(ByValueEqualityTests).GetTypeInfo().Assembly.GetName().GetPublicKey();
            Assert.NotNull(publicKey);
            return publicKey;
        }

        private static byte[] GetPublicKeyTokenFromExecutingAssembly()
        {
            byte[] publicKeyToken = typeof(ByValueEqualityTests).GetTypeInfo().Assembly.GetName().GetPublicKeyToken();
            Assert.NotNull(publicKeyToken);
            return publicKeyToken;
        }

        private static AssemblyName CreateAssemblyName(string name, Version version, CultureInfo cultureInfo, string codeBase, byte[] publicKey = null, byte[] publicKeyToken = null)
        {
            AssemblyName assemblyName = new AssemblyName();
            assemblyName.Name = name;
            assemblyName.Version = version;
#if DESKTOP
            assemblyName.CultureInfo = cultureInfo;
            assemblyName.CodeBase = codeBase;
#endif

            if (publicKey != null)
            {
                assemblyName.SetPublicKey(publicKey);
            }
            else if (publicKeyToken != null)
            {
                assemblyName.SetPublicKeyToken(publicKeyToken);
            }

            return assemblyName;
        }
    }
}
