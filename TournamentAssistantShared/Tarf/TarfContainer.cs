using System;
using System.IO;
using System.IO.Compression;
using ProtoBuf;

namespace TournamentAssistantShared.Tarf
{
    public enum TarfRecordType : byte
    {
        Header = 1,
        MotionBlock = 2,
        EventBatch = 3,
        Footer = 4
    }

    public enum TarfRecordCodec : byte
    {
        None = 0,
        Deflate = 1
    }

    // The binary container around the protobuf records, see TARF/README.md
    public static class TarfContainer
    {
        public static readonly byte[] Magic = { (byte)'T', (byte)'A', (byte)'R', (byte)'F' };
        public const ushort MajorVersion = 1;
        public const ushort MinorVersion = 0;
        public const int FileHeaderLength = 8;
        public const int RecordHeaderLength = 16;

        // Generous limits so a corrupted length can't make a reader allocate gigabytes
        public const int MaxStoredRecordLength = 64 * 1024 * 1024;
        public const int MaxDecodedRecordLength = 256 * 1024 * 1024;

        public static void WriteFileHeader(Stream output)
        {
            var header = new byte[FileHeaderLength];
            Array.Copy(Magic, header, Magic.Length);
            WriteUInt16(header, 4, MajorVersion);
            WriteUInt16(header, 6, MinorVersion);
            output.Write(header, 0, header.Length);
        }

        // Deflate is only kept when it makes the record smaller. Records that are mostly already compressed data, like an embedded map zip, should skip the attempt
        public static void WriteRecord<T>(Stream output, TarfRecordType type, T message, bool tryDeflate = true)
        {
            byte[] payload;
            using (var serialized = new MemoryStream())
            {
                Serializer.Serialize(serialized, message);
                payload = serialized.ToArray();
            }

            var stored = payload;
            var codec = TarfRecordCodec.None;
            if (tryDeflate)
            {
                var compressed = Deflate(payload);
                if (compressed.Length < payload.Length)
                {
                    stored = compressed;
                    codec = TarfRecordCodec.Deflate;
                }
            }

            var header = new byte[RecordHeaderLength];
            header[0] = (byte)type;
            header[1] = (byte)codec;
            WriteUInt16(header, 2, 0);
            WriteUInt32(header, 4, (uint)stored.Length);
            WriteUInt32(header, 8, (uint)payload.Length);
            WriteUInt32(header, 12, Crc32.Compute(payload));

            output.Write(header, 0, header.Length);
            output.Write(stored, 0, stored.Length);
        }

        public static byte[] Deflate(byte[] data)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal))
            {
                deflate.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] Inflate(byte[] data, int decodedLength)
        {
            var decoded = new byte[decodedLength];
            using var deflate = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);

            var read = 0;
            while (read < decodedLength)
            {
                var count = deflate.Read(decoded, read, decodedLength - read);
                if (count == 0) throw new InvalidDataException("TARF record is shorter than its decoded length");
                read += count;
            }
            if (deflate.ReadByte() != -1) throw new InvalidDataException("TARF record is longer than its decoded length");
            return decoded;
        }

        public static ushort ReadUInt16(byte[] buffer, int offset) => (ushort)(buffer[offset] | buffer[offset + 1] << 8);

        public static uint ReadUInt32(byte[] buffer, int offset) => (uint)(buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24);

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
