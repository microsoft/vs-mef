// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class TypeRefTests
    {
        [Fact]
        public void EqualsDistinguishesArrays()
        {
            Assert.NotEqual(TypeRef.Get(typeof(object), TestUtilities.Resolver), TypeRef.Get(typeof(object[]), TestUtilities.Resolver));
        }

        [Fact]
        public void EqualsChecksAssemblyVersionEquality()
        {
            const string assemblyNameFormat = "MyAssembly, Version={0}, Culture=neutral, PublicKeyToken=abcdef1234567890, processorArchitecture=MSIL";
            string assemblyNameV1 = string.Format(assemblyNameFormat, "1.0.0.0");
            string assemblyNameV2 = string.Format(assemblyNameFormat, "2.0.0.0");
            this.TestAssemblyNameEqualityNotEqual(assemblyNameV1, assemblyNameV2, @"C:\MyAssembly.dll", @"C:\MyAssembly.dll");
        }

        [Fact]
        public void EqualsChecksAssemblyPKTEquality()
        {
            const string assemblyNameFormat = "MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken={0}, processorArchitecture=MSIL";
            string assemblyNameV1 = string.Format(assemblyNameFormat, "abcdef1234567890");
            string assemblyNameV2 = string.Format(assemblyNameFormat, "1234567890abcdef");
            this.TestAssemblyNameEqualityNotEqual(assemblyNameV1, assemblyNameV2, @"C:\MyAssembly.dll", @"C:\MyAssembly.dll");
        }

        private void TestAssemblyNameEqualityNotEqual(string assemblyNameV1String, string assemblyNameV2String, string codeBaseV1, string codeBaseV2)
        {
            AssemblyName assemblyNameV1 = new AssemblyName(assemblyNameV1String);
#if DESKTOP
            assemblyNameV1.CodeBase = codeBaseV1;
#endif
            AssemblyName assemblyNameV2 = new AssemblyName(assemblyNameV2String);
#if DESKTOP
            assemblyNameV2.CodeBase = codeBaseV2;
#endif

            TypeRef typeRefV1 = TypeRef.Get(TestUtilities.Resolver, assemblyNameV1, 0x02000001, "SomeType", false, 0, ImmutableArray<TypeRef>.Empty);
            TypeRef typeRefV2 = TypeRef.Get(TestUtilities.Resolver, assemblyNameV2, 0x02000001, "SomeType", false, 0, ImmutableArray<TypeRef>.Empty);

            Assert.NotEqual(typeRefV1, typeRefV2);
        }
    }
}
