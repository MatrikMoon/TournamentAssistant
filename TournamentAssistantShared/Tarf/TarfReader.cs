using System;
using System.IO;
using System.Linq;
using ProtoBuf;
using TournamentAssistantShared.Models.Tarf;

namespace TournamentAssistantShared.Tarf
{
    public static class TarfReader
    {
        public static bool IsTarf(byte[] data)
        {
            if (data == null || data.Length < TarfContainer.FileHeaderLength) return false;
            for (var i = 0; i < TarfContainer.Magic.Length; i++)
            {
                if (data[i] != TarfContainer.Magic[i]) return false;
            }
            return true;
        }

        // A recording which was cut off mid record, for example one that lost its connection, is read up to its last complete record
        public static TarfReplay Read(byte[] data)
        {
            CheckFileHeader(data);

            TarfReplay replay = null;
            var offset = TarfContainer.FileHeaderLength;
            while (TryReadRecord(data, ref offset, out var type, out var payload))
            {
                if (replay == null && type != TarfRecordType.Header)
                {
                    throw new InvalidDataException("The first TARF record must be the header");
                }

                switch (type)
                {
                    case TarfRecordType.Header:
                        if (replay != null) throw new InvalidDataException("TARF file has more than one header");
                        replay = new TarfReplay { Header = Deserialize<Header>(payload) };
                        break;
                    case TarfRecordType.MotionBlock:
                        var block = Deserialize<MotionBlock>(payload);
                        replay.Frames.AddRange(MotionCodec.Unpack(block, replay.Header.MotionEncoding));
                        break;
                    case TarfRecordType.EventBatch:
                        replay.Events.AddRange(Deserialize<EventBatch>(payload).Events);
                        break;
                    case TarfRecordType.Footer:
                        replay.Footer = Deserialize<Footer>(payload);
                        break;
                    default:
                        // Record types from newer minor versions are skipped
                        break;
                }
            }

            if (replay == null) throw new InvalidDataException("TARF file has no header");

            // Live recordings write events as they are known, so batches can overlap in time
            replay.Events = replay.Events.OrderBy(x => x.TimeUs).ToList();
            return replay;
        }

        // Reads only the first record, which is enough to list or index a replay without decoding its motion
        public static Header ReadHeader(byte[] data) => ReadHeader(data, out _);

        // headerEnd is where the record after the header starts
        public static Header ReadHeader(byte[] data, out int headerEnd)
        {
            CheckFileHeader(data);

            headerEnd = TarfContainer.FileHeaderLength;
            if (!TryReadRecord(data, ref headerEnd, out var type, out var payload) || type != TarfRecordType.Header)
            {
                throw new InvalidDataException("The first TARF record must be the header");
            }
            return Deserialize<Header>(payload);
        }

        private static void CheckFileHeader(byte[] data)
        {
            if (!IsTarf(data)) throw new InvalidDataException("Not a TARF file");

            var majorVersion = TarfContainer.ReadUInt16(data, 4);
            if (majorVersion != TarfContainer.MajorVersion)
            {
                throw new NotSupportedException($"TARF version {majorVersion} is not supported");
            }
        }

        private static bool TryReadRecord(byte[] data, ref int offset, out TarfRecordType type, out byte[] payload)
        {
            type = 0;
            payload = null;
            if (data.Length - offset < TarfContainer.RecordHeaderLength) return false;

            type = (TarfRecordType)data[offset];
            var codec = (TarfRecordCodec)data[offset + 1];
            var storedLength = TarfContainer.ReadUInt32(data, offset + 4);
            var decodedLength = TarfContainer.ReadUInt32(data, offset + 8);
            var checksum = TarfContainer.ReadUInt32(data, offset + 12);

            if (storedLength > TarfContainer.MaxStoredRecordLength || decodedLength > TarfContainer.MaxDecodedRecordLength)
            {
                throw new InvalidDataException("TARF record is too large");
            }

            var payloadStart = offset + TarfContainer.RecordHeaderLength;
            if (data.Length - payloadStart < storedLength) return false;

            var stored = new byte[storedLength];
            Array.Copy(data, payloadStart, stored, 0, stored.Length);

            switch (codec)
            {
                case TarfRecordCodec.None:
                    if (storedLength != decodedLength) throw new InvalidDataException("TARF record length mismatch");
                    payload = stored;
                    break;
                case TarfRecordCodec.Deflate:
                    payload = TarfContainer.Inflate(stored, (int)decodedLength);
                    break;
                default:
                    throw new NotSupportedException($"Unknown TARF record codec {codec}");
            }

            if (Crc32.Compute(payload) != checksum)
            {
                throw new InvalidDataException($"TARF record at offset {offset} failed its checksum");
            }

            offset = payloadStart + (int)storedLength;
            return true;
        }

        private static T Deserialize<T>(byte[] payload)
        {
            using var stream = new MemoryStream(payload);
            return Serializer.Deserialize<T>(stream);
        }
    }
}
