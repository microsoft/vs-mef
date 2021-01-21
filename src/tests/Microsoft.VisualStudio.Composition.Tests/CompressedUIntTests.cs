// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class CompressedUIntTests
    {
        private readonly ITestOutputHelper output;

        public CompressedUIntTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void CompressedUIntReadWrite()
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            var reader = new BinaryReader(ms);
            try
            {
                for (uint i = 0; i < uint.MaxValue; i = checked((i * 5) + 1))
                {
                    ////this.output.WriteLine("0x{0:x8} {0,7}", i);
                    Test(i, writer, reader);
                }
            }
            catch (OverflowException)
            {
                // the for loop stepping function itself throws this to exit.
            }

            Test(uint.MaxValue, writer, reader);
        }

        private static void Test(uint value, BinaryWriter writer, BinaryReader reader)
        {
            writer.BaseStream.Position = 0;
            CompressedUInt.WriteCompressedUInt(writer, value);
            writer.Flush();
            writer.BaseStream.Position = 0;
            uint reread = CompressedUInt.ReadCompressedUInt(reader);
            Assert.Equal(value, reread);
        }
    }
}
