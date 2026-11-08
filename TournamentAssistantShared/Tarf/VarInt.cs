using System.IO;

namespace TournamentAssistantShared.Tarf
{
    // Signed integers are ZigZag mapped to unsigned ones and then written as base 128 varints, the same way protobuf encodes sint64 fields
    public static class VarInt
    {
        public static void WriteSigned(Stream stream, long value) => WriteUnsigned(stream, (ulong)((value << 1) ^ (value >> 63)));

        public static void WriteUnsigned(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        public static long ReadSigned(byte[] buffer, ref int offset)
        {
            var zigZag = ReadUnsigned(buffer, ref offset);
            return (long)(zigZag >> 1) ^ -(long)(zigZag & 1);
        }

        public static ulong ReadUnsigned(byte[] buffer, ref int offset)
        {
            ulong value = 0;
            var shift = 0;
            while (true)
            {
                if (offset >= buffer.Length) throw new InvalidDataException("Truncated varint");
                if (shift > 63) throw new InvalidDataException("Varint is too long");

                var current = buffer[offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0) return value;
                shift += 7;
            }
        }
    }
}
