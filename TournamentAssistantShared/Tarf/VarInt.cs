using System.IO;

namespace TournamentAssistantShared.Tarf
{
    // Signed integers are ZigZag mapped to unsigned ones and then written as base 128 varints, the same way protobuf encodes sint64 fields
    public static class VarInt
    {
        public static void WriteSigned(Stream stream, long value)
        {
            var zigZag = (ulong)((value << 1) ^ (value >> 63));
            while (zigZag >= 0x80)
            {
                stream.WriteByte((byte)(zigZag | 0x80));
                zigZag >>= 7;
            }
            stream.WriteByte((byte)zigZag);
        }

        public static long ReadSigned(byte[] buffer, ref int offset)
        {
            ulong zigZag = 0;
            var shift = 0;
            while (true)
            {
                if (offset >= buffer.Length) throw new InvalidDataException("Truncated varint");
                if (shift > 63) throw new InvalidDataException("Varint is too long");

                var current = buffer[offset++];
                zigZag |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0) break;
                shift += 7;
            }
            return (long)(zigZag >> 1) ^ -(long)(zigZag & 1);
        }
    }
}
