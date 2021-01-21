// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class CompressedUInt
    {
        internal static void WriteCompressedUInt(BinaryWriter writer, uint value)
        {
            if (value <= 0x3f) // 6 bit value: leading "00" MSBs
            {
                writer.Write((byte)value);
            }
            else if (value <= 0x3fff) // 14 bit value: leading "01" MSBs
            {
                writer.Write((byte)((value >> 8) | 0x40));
                writer.Write((byte)value);
            }
            else if (value <= 0x3fffff) // 22 bit value: leading "10" MSBs
            {
                // Write this out as two steps: the "10" prefix and the 6 MSBs first,
                // Then write out the 16 LSBs.
                writer.Write((byte)((value >> 16) | 0x80));
                writer.Write((byte)(value >> 8));
                writer.Write((byte)value);
            }
            else // > 22 bit value: leading 0xc0 byte ("11" MSBs) followed by four bytes
            {
                writer.Write((byte)0xc0);
                writer.Write(value);
            }
        }

        internal static uint ReadCompressedUInt(BinaryReader reader)
        {
            byte firstByte = reader.ReadByte();
            byte leadingTwoBits = (byte)(firstByte & 0xc0);
            byte firstByteWithoutTwoMSBs = (byte)(firstByte & 0x3f);
            uint result;
            switch (leadingTwoBits)
            {
                case 0x00: // 00: 6 bit number
                    result = firstByte;
                    break;
                case 0x40: // 01: 14 bit number
                    byte secondByte = reader.ReadByte();
                    result = (uint)(firstByteWithoutTwoMSBs << 8) | secondByte;
                    break;
                case 0x80: // 10: 22 bit number
                    result = (uint)firstByteWithoutTwoMSBs << 16;
                    result |= (uint)reader.ReadByte() << 8;
                    result |= reader.ReadByte();
                    break;
                case 0xc0: // 11: > 22 bit number
                    result = reader.ReadUInt32();
                    break;
                default:
                    throw new NotSupportedException();
            }

            return result;
        }
    }
}
