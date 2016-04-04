namespace Microsoft.VisualStudio.Composition.Tests.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class ResolverExtensionsTests
    {
        [Theory, MemberData(nameof(GetResolveTestData))]
        public void Resolve_ThrowsCompositionFailedException(Exception exceptionToBeWrapped, MemberRef memberToResolve)
        {
            try
            {
                memberToResolve.Resolve();
                Assert.True(false, "Did not expect resolution to complete successfully.");
            }
            catch (CompositionFailedException compositionFailedException)
            {
                Assert.NotNull(compositionFailedException.InnerException);
                Assert.Equal(exceptionToBeWrapped, compositionFailedException.InnerException);
            }
        }

        private static readonly Exception[] ExceptionsToTest = new Exception[] { new BadImageFormatException(), new FileLoadException(), new FileNotFoundException() };

        public static IEnumerable<object[]> GetResolveTestData()
        {
            List<object[]> data = new List<object[]>();
            Type dummyClassType = typeof(DummyClass);

            foreach (var exception in ExceptionsToTest)
            {
                Resolver resolver = new Resolver(new MockAssemblyLoader(exception));
                data.Add(new object[]
                {
                    exception,
                    new MemberRef(ConstructorRef.Get(dummyClassType.GetConstructor(new Type[0]), resolver))
                });

                data.Add(new object[]
                {
                    exception,
                    new MemberRef(new FieldRef(dummyClassType.GetField(nameof(DummyClass.DummyField), BindingFlags.Public | BindingFlags.Instance), resolver))
                });

                data.Add(new object[]
                {
                    exception,
                    new MemberRef(new PropertyRef(dummyClassType.GetProperty(nameof(DummyClass.DummyProperty), BindingFlags.Public | BindingFlags.Instance), resolver))
                });

                data.Add(new object[]
                {
                    exception,
                    new MemberRef(new MethodRef(dummyClassType.GetMethod(nameof(DummyClass.DummyMethod), BindingFlags.Public | BindingFlags.Instance), resolver))
                });

                data.Add(new object[]
                {
                    exception,
                    new MemberRef(TypeRef.Get(dummyClassType, resolver))
                });
            }

            return data;
        }

        private class DummyClass
        {
            public DummyClass() { }

            public void DummyMethod(object param1) { }

            public int DummyProperty { get { return 0; } set { } }

            public int DummyField = 1;
        }

        private class MockAssemblyLoader : IAssemblyLoader
        {
            private Exception exceptionToThrow;

            public MockAssemblyLoader(Exception e)
            {
                this.exceptionToThrow = e;
            }

            public Assembly LoadAssembly(AssemblyName assemblyName)
            {
                throw this.exceptionToThrow;
            }

            public Assembly LoadAssembly(string assemblyFullName, string codeBasePath)
            {
                throw this.exceptionToThrow;
            }
        }
    }
}
