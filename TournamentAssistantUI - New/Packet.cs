using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using TournamentAssistantShared.SimpleJSON;

/**
 * Created by Moon 7/2/2019
 * Generic Packet class, handles converting any serializable class to/from bytes
 * and reading such classes from bytes or a stream
 */

namespace TournamentAssistantShared
{
    [Serializable]
    public class Packet
    {
        public enum PacketType
        {
            Acknowledgement,
            Command,
            Connect,
            ConnectResponse,
            Event,
            File,
            ForwardingPacket,
            LoadedSong,
            LoadSong,
            PlaySong,
            Response,
            ScoreRequest,
            ScoreRequestResponse,
            SongFinished,
            SongList,
            SubmitScore
        }

        //Size of the header, the info we need to parse the specific packet
        // 4x byte - "moon"
        // int - packet type
        // int - packet size
        // 16x byte - size of from id
        // 16x byte - size of packet id
        public const int packetHeaderSize = (sizeof(int) * 2) + (sizeof(byte) * 4) + (sizeof(byte) * 16) + (sizeof(byte) * 16);

        public int Size => SpecificPacketSize + packetHeaderSize;
        public int SpecificPacketSize { get; private set; }
        public Guid Id { get; private set; }
        public Guid From { get; set; }
        public PacketType Type { get; private set; }
        public object SpecificPacket { get; private set; }

        public Packet(object specificPacket)
        {
            //Assign type based on parameter type
            Type = (PacketType)Enum.Parse(typeof(PacketType), specificPacket.GetType().Name);
            SpecificPacket = specificPacket;
        }

        public byte[] ToBytes()
        {
            Id = Guid.NewGuid();
            byte[] specificPacketBytes = null;

            using (var memoryStream = new MemoryStream())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Binder = new CustomSerializationBinder();
                binaryFormatter.Serialize(memoryStream, SpecificPacket);
                specificPacketBytes = memoryStream.ToArray();
            }

            var magicFlag = Encoding.UTF8.GetBytes("moon");
            var typeBytes = BitConverter.GetBytes((int)Type);
            var sizeBytes = BitConverter.GetBytes(specificPacketBytes.Length);
            var fromBytes = From.ToByteArray();
            var idBytes = Id.ToByteArray();

            return Combine(magicFlag, typeBytes, sizeBytes, fromBytes, idBytes, specificPacketBytes);
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

        public static Packet FromBytesJson(byte[] bytes)
        {
            Packet returnPacket;
            using (var stream = new MemoryStream(bytes))
            {
                returnPacket = FromStreamJson(stream);
            }
            return returnPacket;
        }

        public static Packet FromStream(MemoryStream stream)
        {
            var typeBytes = new byte[sizeof(int)];
            var sizeBytes = new byte[sizeof(int)];
            var fromBytes = new byte[16];
            var idBytes = new byte[16];

            //Verify that this is indeed a Packet
            if (!StreamIsAtPacket(stream, false))
            {
                stream.Seek(-(sizeof(byte) * 4), SeekOrigin.Current); //Return to original position in stream
                return null;
            }

            stream.Read(typeBytes, 0, typeBytes.Length);
            stream.Read(sizeBytes, 0, sizeBytes.Length);
            stream.Read(fromBytes, 0, fromBytes.Length);
            stream.Read(idBytes, 0, idBytes.Length);

            var specificPacketSize = BitConverter.ToInt32(sizeBytes, 0);
            object specificPacket = null;

            //There needn't mecessarily be a specific packet for every packet (acks)
            if (specificPacketSize > 0)
            {
                var specificPacketBytes = new byte[specificPacketSize];

                stream.Read(specificPacketBytes, 0, specificPacketBytes.Length);

                using (var memStream = new MemoryStream())
                {
                    memStream.Write(specificPacketBytes, 0, specificPacketBytes.Length);
                    memStream.Seek(0, SeekOrigin.Begin);

                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    binaryFormatter.Binder = new CustomSerializationBinder();
                    specificPacket = binaryFormatter.Deserialize(memStream);
                }
            }

            return new Packet(specificPacket)
            {
                SpecificPacketSize = specificPacketSize,
                Type = (PacketType)BitConverter.ToInt32(typeBytes, 0),
                From = new Guid(fromBytes),
                Id = new Guid(idBytes)
            };
        }

        public static Packet FromStreamJson(MemoryStream stream)
        {
            var typeBytes = new byte[sizeof(int)];
            var sizeBytes = new byte[sizeof(int)];
            var fromBytes = new byte[16];
            var idBytes = new byte[16];

            //Verify that this is indeed a Packet
            if (!StreamIsAtPacket(stream, false))
            {
                stream.Seek(-(sizeof(byte) * 4), SeekOrigin.Current); //Return to original position in stream
                return null;
            }

            stream.Read(typeBytes, 0, typeBytes.Length);
            stream.Read(sizeBytes, 0, sizeBytes.Length);
            stream.Read(fromBytes, 0, fromBytes.Length);
            stream.Read(idBytes, 0, idBytes.Length);

            var specificPacketSize = BitConverter.ToInt32(sizeBytes, 0);
            object specificPacket = null;

            //There needn't necessarily be a specific packet for every packet (acks)
            if (specificPacketSize > 0)
            {
                var specificPacketBytes = new byte[specificPacketSize];
                Logger.Debug(specificPacketBytes.Length.ToString());
                stream.Read(specificPacketBytes, 0, specificPacketBytes.Length);

                var json = Encoding.UTF8.GetString(specificPacketBytes);
                Logger.Debug(json);
                var typeInt = BitConverter.ToInt32(typeBytes, 0);
                var typeString = ((PacketType)typeInt).ToString();
                var packetType = System.Type.GetType($"TournamentAssistantShared.Models.Packets.{typeString}");
                specificPacket = JsonConvert.DeserializeObject(json.ToString(), packetType);
            }

            return new Packet(specificPacket)
            {
                SpecificPacketSize = specificPacketSize,
                Type = (PacketType)BitConverter.ToInt32(typeBytes, 0),
                From = new Guid(fromBytes),
                Id = new Guid(idBytes)
            };
        }

        public static Packet FromJSON(string json)
        {
            Logger.Debug("Overlay: " + json);

            var parsedJson = JSON.Parse(json);

            var typeNumber = parsedJson["Type"].AsInt;
            var packetType = System.Type.GetType($"TournamentAssistantShared.Models.Packets.{(PacketType)typeNumber}");

            var specificPacketJson = parsedJson["SpecificPacket"].AsObject.ToString();
            var specificPacket = JsonConvert.DeserializeObject(specificPacketJson, packetType);

            var specificPacketSize = parsedJson["SpecificPacketSize"].AsInt;
            var from = Guid.Parse(parsedJson["From"].Value);
            var id = Guid.Parse(parsedJson["Id"].Value);

            return new Packet(specificPacket)
            {
                SpecificPacketSize = specificPacketSize,
                Type = (PacketType)typeNumber,
                From = from,
                Id = id
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
                var typeBytes = new byte[sizeof(int)];
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
                    stream.Read(typeBytes, 0, typeBytes.Length);
                    stream.Read(sizeBytes, 0, sizeBytes.Length);
                    stream.Read(fromBytes, 0, fromBytes.Length);
                    stream.Read(idBytes, 0, idBytes.Length);

                    stream.Seek(-(sizeof(byte) * 4 + typeBytes.Length + sizeBytes.Length + fromBytes.Length + idBytes.Length), SeekOrigin.Current); //Return to original position in stream

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
