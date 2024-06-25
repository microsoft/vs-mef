// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.IO;
    using MessagePack;
    using Xunit;

    public class PartDiscoveryExceptionTests
    {
#if !NETCOREAPP
        [Fact]
        public void ExceptionIsSerializable_BinaryFormatter()
        {
            var exception = new PartDiscoveryException("msg") { AssemblyPath = "/some path", ScannedType = typeof(string) };

            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, exception);

            ms.Position = 0;
            var actual = (PartDiscoveryException)formatter.Deserialize(ms);
            Assert.Equal(exception!.Message, actual.Message);
            Assert.Equal(exception.ScannedType, actual.ScannedType);
            Assert.Equal(exception.AssemblyPath, actual.AssemblyPath);
        }
#endif

        [Fact]
        public void ExceptionIsSerializable()
        {
            var exceptionToTest = new PartDiscoveryException("msg") { AssemblyPath = "/some path", ScannedType = typeof(string) };

            var context = new MessagePackSerializerContext(Resolver.DefaultInstance);
            var ms = new MemoryStream();
            MessagePackSerializer.Serialize(ms, exceptionToTest, context.DefaultOptions);
            ms.Position = 0;
            PartDiscoveryException actual = MessagePackSerializer.Deserialize<PartDiscoveryException>(ms, context.DefaultOptions);

            Assert.Equal(exceptionToTest!.Message, actual.Message);
            Assert.Equal(exceptionToTest.ScannedType, actual.ScannedType);
            Assert.Equal(exceptionToTest.AssemblyPath, actual.AssemblyPath);
        }

        [Fact]
        public void ExceptionIsSerializableWithInnerException()
        {
            Exception innerException = new InvalidOperationException("inner");
            var exceptionToTest = new PartDiscoveryException("msg", innerException) { AssemblyPath = "/some path", ScannedType = typeof(string) };

            var context = new MessagePackSerializerContext(Resolver.DefaultInstance);
            var ms = new MemoryStream();
            MessagePackSerializer.Serialize(ms, exceptionToTest, context.DefaultOptions);
            ms.Position = 0;
            PartDiscoveryException actual = MessagePackSerializer.Deserialize<PartDiscoveryException>(ms, context.DefaultOptions);

            Assert.Equal(exceptionToTest.Message, actual.Message);
            Assert.Equal(exceptionToTest.ScannedType, actual.ScannedType);
            Assert.Equal(exceptionToTest.AssemblyPath, actual.AssemblyPath);
            Assert.NotNull(actual.InnerException);
            Assert.Equal(exceptionToTest.InnerException!.Message, actual.InnerException.Message);
        }

        [Fact]
        public void ExceptionIsSerializableWithInnerExceptionWithMaxDepthTwo()
        {
            Exception innerException4 = new InvalidOperationException("inner4");
            Exception innerException3 = new InvalidOperationException("inner3", innerException4);
            Exception innerException2 = new InvalidOperationException("inner2", innerException3);
            Exception innerException1 = new InvalidOperationException("inner1", innerException2);

            var exceptionToTest = new PartDiscoveryException("msg", innerException1) { AssemblyPath = "/some path", ScannedType = typeof(string) };

            var context = new MessagePackSerializerContext(Resolver.DefaultInstance);
            var ms = new MemoryStream();
            MessagePackSerializer.Serialize(ms, exceptionToTest, context.DefaultOptions);
            ms.Position = 0;
            PartDiscoveryException actual = MessagePackSerializer.Deserialize<PartDiscoveryException>(ms, context.DefaultOptions);

            Assert.Equal(exceptionToTest!.Message, actual.Message);
            Assert.Equal(exceptionToTest.ScannedType, actual.ScannedType);
            Assert.Equal(exceptionToTest.AssemblyPath, actual.AssemblyPath);

            Assert.NotNull(actual.InnerException);
            Assert.Equal(exceptionToTest.InnerException!.Message, actual.InnerException.Message);

            Assert.NotNull(actual.InnerException.InnerException);
            Assert.Equal(exceptionToTest.InnerException!.InnerException!.Message, actual.InnerException.InnerException.Message);

            Assert.NotNull(actual.InnerException.InnerException.InnerException);
            Assert.Equal(exceptionToTest.InnerException.InnerException.InnerException!.Message, actual.InnerException.InnerException.InnerException.Message);

            Assert.Null(actual.InnerException.InnerException.InnerException.InnerException);
        }

        [Fact]
        public void ExceptionIsSerializableWithStackTrace()
        {
            PartDiscoveryException exceptionToTest;

            try
            {
                throw new PartDiscoveryException("msg") { AssemblyPath = "/some path", ScannedType = typeof(string) };
            }
            catch (PartDiscoveryException ex)
            {
                exceptionToTest = ex;
            }

            var context = new MessagePackSerializerContext(Resolver.DefaultInstance);
            var ms = new MemoryStream();
            MessagePackSerializer.Serialize(ms, exceptionToTest, context.DefaultOptions);
            ms.Position = 0;
            PartDiscoveryException actual = MessagePackSerializer.Deserialize<PartDiscoveryException>(ms, context.DefaultOptions);

            Assert.Equal(exceptionToTest!.Message, actual.Message);
            Assert.Equal(exceptionToTest.ScannedType, actual.ScannedType);
            Assert.Equal(exceptionToTest.AssemblyPath, actual.AssemblyPath);
            Assert.Equal(exceptionToTest.StackTrace, actual.StackTrace);
        }
    }
}
