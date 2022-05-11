using System;
using System.IO;
using System.Linq;
using System.Text;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;

/**
 * Created by Moon 7/2/2019
 * Generic Packet class, handles converting any serializable class to/from bytes
 * and reading such classes from bytes or a stream
 */

namespace TournamentAssistantShared
{
    public class PacketWrapper
    {
        //Size of the header, the info we need to parse the specific packet
        // 4x byte - "moon"
        // int - packet size
        public const int packetHeaderSize = (sizeof(byte) * 4) + sizeof(int);

        public int Size => SpecificPacketSize + packetHeaderSize;
        public int SpecificPacketSize { get; private set; }
        public Packet Payload { get; private set; }

        public PacketWrapper(Packet payload)
        {
            payload.Id = Guid.NewGuid().ToString();
            Payload = payload;
        }

        public byte[] ToBytes()
        {
            byte[] magicBytes = Encoding.UTF8.GetBytes("moon");
            byte[] payloadBytes = Payload.ProtoSerialize();
            var payloadLength = BitConverter.GetBytes(payloadBytes.Length);
            return Combine(magicBytes, payloadLength, payloadBytes);
        }

        public static PacketWrapper FromBytes(byte[] bytes)
        {
            PacketWrapper returnPacket;
            using (var stream = new MemoryStream(bytes))
            {
                returnPacket = FromStream(stream);
            }
            return returnPacket;
        }

        public static PacketWrapper FromStream(MemoryStream stream)
        {
            var sizeBytes = new byte[sizeof(int)];

            //Verify that this is indeed a Packet
            if (!StreamIsAtPacket(stream, false))
            {
                stream.Seek(-(sizeof(byte) * 4), SeekOrigin.Current); //Return to original position in stream
                return null;
            }

            stream.Read(sizeBytes, 0, sizeBytes.Length);

            var payloadSize = BitConverter.ToInt32(sizeBytes, 0);
            Packet payload = null;

            //There needn't necessarily be a specific packet for every packet (acks)
            if (payloadSize > 0)
            {
                var payloadBytes = new byte[payloadSize];
                stream.Read(payloadBytes, 0, payloadBytes.Length);

                payload = payloadBytes.ProtoDeserialize<Packet>();
            }

            return new PacketWrapper(payload)
            {
                SpecificPacketSize = payloadSize
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

                //Verify that this is indeed a Packet
                if (!StreamIsAtPacket(stream, false))
                {
                    stream.Seek(-(sizeof(byte) * 4), SeekOrigin.Current); //Return to original position in stream
                }
                else
                {
                    stream.Read(sizeBytes, 0, sizeBytes.Length);

                    stream.Seek(-(sizeof(byte) * 4 + sizeBytes.Length), SeekOrigin.Current); //Return to original position in stream

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