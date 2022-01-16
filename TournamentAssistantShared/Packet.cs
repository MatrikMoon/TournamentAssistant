using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System;
using System.IO;
using System.Linq;
using System.Text;
using TournamentAssistantShared.SimpleJSON;

/**
 * Created by Moon 7/2/2019
 * Generic Packet class, handles converting any serializable class to/from bytes
 * and reading such classes from bytes or a stream
 */

namespace TournamentAssistantShared
{
    public class Packet
    {
        //Size of the header, the info we need to parse the specific packet
        // 4x byte - "moon"
        // int - packet size
        // 16x byte - from id
        // 16x byte - packet id
        public const int packetHeaderSize = (sizeof(byte) * 4) + sizeof(int) + (sizeof(byte) * 16) + (sizeof(byte) * 16);

        public int Size => SpecificPacketSize + packetHeaderSize;
        public int SpecificPacketSize { get; private set; }
        public Guid Id { get; private set; }
        public Guid From { get; set; }
        public Any SpecificPacket { get; private set; }

        public Packet(Any specificPacket)
        {
            SpecificPacket = specificPacket;
        }

        public Packet(IMessage specificPacket)
        {
            SpecificPacket = Any.Pack(specificPacket);
        }

        public byte[] ToBytes()
        {
            Id = Guid.NewGuid();
            byte[] specificPacketBytes = SpecificPacket.ToByteArray();

            var magicFlag = Encoding.UTF8.GetBytes("moon");
            var sizeBytes = BitConverter.GetBytes(specificPacketBytes.Length);
            var fromBytes = From.ToByteArray();
            var idBytes = Id.ToByteArray();

            return Combine(magicFlag, sizeBytes, fromBytes, idBytes, specificPacketBytes);
        }

        public string ToBase64() => Convert.ToBase64String(ToBytes());

        public static Packet FromBytes(byte[] bytes)
        {
            Packet returnPacket;
            using (var stream = new MemoryStream(bytes))
            {
                returnPacket = FromStream(stream);
            }
            return returnPacket;
        }

        public static Packet FromJSON(string json)
        {
            var parsedJson = JSON.Parse(json);
            var specificPacketBytes = ByteString.FromBase64(parsedJson["SpecificPacket"].AsObject.ToString());

            var from = Guid.Parse(parsedJson["From"].Value);
            var id = Guid.Parse(parsedJson["Id"].Value);

            var proto = Any.Parser.ParseFrom(specificPacketBytes);

            return new Packet(proto)
            {
                SpecificPacketSize = specificPacketBytes.Length,
                From = from,
                Id = id
            };
        }

        public static Packet FromStream(MemoryStream stream)
        {
            var sizeBytes = new byte[sizeof(int)];
            var fromBytes = new byte[16];
            var idBytes = new byte[16];

            //Verify that this is indeed a Packet
            if (!StreamIsAtPacket(stream, false))
            {
                stream.Seek(-(sizeof(byte) * 4), SeekOrigin.Current); //Return to original position in stream
                return null;
            }

            stream.Read(sizeBytes, 0, sizeBytes.Length);
            stream.Read(fromBytes, 0, fromBytes.Length);
            stream.Read(idBytes, 0, idBytes.Length);

            var specificPacketSize = BitConverter.ToInt32(sizeBytes, 0);
            Any specificPacket = null;

            //There needn't necessarily be a specific packet for every packet (acks)
            if (specificPacketSize > 0)
            {
                var specificPacketBytes = new byte[specificPacketSize];
                stream.Read(specificPacketBytes, 0, specificPacketBytes.Length);

                specificPacket = Any.Parser.ParseFrom(specificPacketBytes);
            }

            return new Packet(specificPacket)
            {
                SpecificPacketSize = specificPacketSize,
                From = new Guid(fromBytes),
                Id = new Guid(idBytes)
            };
        }

        public static bool StreamIsAtPacket(byte[] bytes, bool resetStreamPos = true)
        {
            bool returnValue = false;
            using (var stream = new MemoryStream(bytes))
            {
                returnValue = StreamIsAtPacket(stream, resetStreamPos);
            }
            return returnValue;
        }

        public static bool StreamIsAtPacket(MemoryStream stream, bool resetStreamPos = true)
        {
            var magicFlagBytes = new byte[sizeof(byte) * 4];

            //Verify that this is indeed a Packet
            stream.Read(magicFlagBytes, 0, magicFlagBytes.Length);

            if (resetStreamPos) stream.Seek(-magicFlagBytes.Length, SeekOrigin.Current); //Return to original position in stream

            return Encoding.UTF8.GetString(magicFlagBytes) == "moon";
        }

        public static bool PotentiallyValidPacket(byte[] bytes)
        {
            var returnValue = false;
            using (var stream = new MemoryStream(bytes))
            {
                var sizeBytes = new byte[sizeof(int)];
                var fromBytes = new byte[16];
                var idBytes = new byte[16];

                //Verify that this is indeed a Packet
                if (!StreamIsAtPacket(stream, false))
                {
                    stream.Seek(-(sizeof(byte) * 4), SeekOrigin.Current); //Return to original position in stream
                }
                else
                {
                    stream.Read(sizeBytes, 0, sizeBytes.Length);
                    stream.Read(fromBytes, 0, fromBytes.Length);
                    stream.Read(idBytes, 0, idBytes.Length);

                    stream.Seek(-(sizeof(byte) * 4 + sizeBytes.Length + fromBytes.Length + idBytes.Length), SeekOrigin.Current); //Return to original position in stream

                    returnValue = (BitConverter.ToInt32(sizeBytes, 0) + packetHeaderSize) <= bytes.Length;
                }
            }
            return returnValue;
        }

        private static byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }
}