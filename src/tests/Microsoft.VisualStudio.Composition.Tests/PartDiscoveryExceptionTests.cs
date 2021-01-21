// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System.IO;
    using Xunit;

    public class PartDiscoveryExceptionTests
    {
#if !NETCOREAPP
        [Fact]
        public void ExceptionIsSerializable()
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
    }
}
