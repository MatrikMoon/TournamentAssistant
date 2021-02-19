using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
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

        private Packet()
        {
        }

        public Packet(object specificPacket)
        {
            Type = (PacketType)System.Enum.Parse(typeof(PacketType), specificPacket.GetType().Name);
            SpecificPacket = specificPacket;
            Logger.Debug("Creating packet with instance: " + specificPacket);
        }

        public byte[] ToBytes()
        {
            Id = Guid.NewGuid();
            var magicFlag = Encoding.UTF8.GetBytes("moon");
            var typeBytes = BitConverter.GetBytes((int)Type);
            var fromBytes = From.ToByteArray();
            var idBytes = Id.ToByteArray();

            if (SpecificPacket != null)
            {
                var specificPacketBytes = (SpecificPacket as IMessage).ToByteArray();
                var sizeBytes = BitConverter.GetBytes(specificPacketBytes.Length);

                return Combine(magicFlag, typeBytes, sizeBytes, fromBytes, idBytes, specificPacketBytes);
            }
            return Combine(magicFlag, typeBytes, new byte[4] { 0, 0, 0, 0 }, fromBytes, idBytes);
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

            var pktType = (PacketType)BitConverter.ToInt32(typeBytes, 0);

            object specificPacket = null;

            if (specificPacketSize > 0)
            {
                var specificPacketBytes = new byte[specificPacketSize];

                stream.Read(specificPacketBytes, 0, specificPacketBytes.Length);

                using (var memStream = new MemoryStream())
                {
                    memStream.Write(specificPacketBytes, 0, specificPacketBytes.Length);
                    memStream.Seek(0, SeekOrigin.Begin);

                    switch (pktType)
                    {
                        case PacketType.Command:
                            specificPacket = Command.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.Connect:
                            specificPacket = Connect.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.ConnectResponse:
                            specificPacket = ConnectResponse.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.Event:
                            specificPacket = Event.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.File:
                            specificPacket = Models.Packets.File.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.ForwardingPacket:
                            specificPacket = ForwardingPacket.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.LoadedSong:
                            specificPacket = LoadedSong.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.LoadSong:
                            specificPacket = LoadSong.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.PlaySong:
                            specificPacket = PlaySong.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.Response:
                            specificPacket = Response.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.ScoreRequest:
                            specificPacket = ScoreRequest.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.ScoreRequestResponse:
                            specificPacket = ScoreRequestResponse.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.SongFinished:
                            specificPacket = SongFinished.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.SongList:
                            specificPacket = SongList.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.SubmitScore:
                            specificPacket = SubmitScore.Parser.ParseFrom(memStream);
                            break;

                        case PacketType.Acknowledgement:
                            specificPacket = null;
                            break;

                        default:
                            throw new InvalidOperationException("Unsupported packet type!");
                    }
                }
            }

            return new Packet
            {
                SpecificPacketSize = specificPacketSize,
                Type = pktType,
                From = new Guid(fromBytes),
                Id = new Guid(idBytes),
                SpecificPacket = specificPacket
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