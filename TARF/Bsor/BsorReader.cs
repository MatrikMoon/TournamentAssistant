using System.Text;
using TournamentAssistantShared.Tarf;

namespace TARF.Bsor
{
    // Mirrors ReplayDecoder from the BeatLeader PC mod.
    public class BsorReader
    {
        private readonly BinaryReader _reader;

        private BsorReader(byte[] data)
        {
            _reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
        }

        public static bool IsBsor(byte[] data)
        {
            return data.Length >= 5 && BitConverter.ToInt32(data, 0) == BsorReplay.Magic;
        }

        public static BsorReplay Read(byte[] data)
        {
            return new BsorReader(data).ReadReplay();
        }

        private BsorReplay ReadReplay()
        {
            if (_reader.ReadInt32() != BsorReplay.Magic) throw new InvalidDataException("Not a BSOR file");

            var version = _reader.ReadByte();
            if (version != BsorReplay.Version) throw new NotSupportedException($"BSOR version {version} is not supported");

            var replay = new BsorReplay();
            while (_reader.BaseStream.Position < _reader.BaseStream.Length)
            {
                var section = (BsorSection)_reader.ReadByte();
                switch (section)
                {
                    case BsorSection.Info:
                        replay.Info = ReadInfo();
                        break;
                    case BsorSection.Frames:
                        replay.Frames = ReadList(ReadFrame);
                        break;
                    case BsorSection.Notes:
                        replay.Notes = ReadList(ReadNoteEvent);
                        break;
                    case BsorSection.Walls:
                        replay.Walls = ReadList(ReadWallEvent);
                        break;
                    case BsorSection.Heights:
                        replay.Heights = ReadList(ReadHeightEvent);
                        break;
                    case BsorSection.Pauses:
                        replay.Pauses = ReadList(ReadPauseEvent);
                        break;
                    case BsorSection.SaberOffsets:
                        replay.SaberOffsets = new BsorSaberOffsets { Left = ReadPose(), Right = ReadPose() };
                        break;
                    case BsorSection.CustomData:
                        replay.CustomData = ReadCustomData();
                        break;
                    default:
                        throw new InvalidDataException($"Unknown BSOR section {(byte)section}");
                }
            }
            return replay;
        }

        private BsorInfo ReadInfo()
        {
            return new BsorInfo
            {
                ModVersion = ReadString(),
                GameVersion = ReadString(),
                Timestamp = ReadString(),

                PlayerId = ReadString(),
                PlayerName = ReadPlayerName(),
                Platform = ReadString(),

                TrackingSystem = ReadString(),
                Hmd = ReadString(),
                Controller = ReadString(),

                Hash = ReadString(),
                SongName = ReadString(),
                Mapper = ReadString(),
                Difficulty = ReadString(),

                Score = _reader.ReadInt32(),
                Mode = ReadString(),
                Environment = ReadString(),
                Modifiers = ReadString(),
                JumpDistance = _reader.ReadSingle(),
                LeftHanded = _reader.ReadBoolean(),
                Height = _reader.ReadSingle(),

                StartTime = _reader.ReadSingle(),
                FailTime = _reader.ReadSingle(),
                Speed = _reader.ReadSingle()
            };
        }

        private BsorFrame ReadFrame()
        {
            return new BsorFrame
            {
                Time = _reader.ReadSingle(),
                Fps = _reader.ReadInt32(),
                Head = ReadPose(),
                LeftHand = ReadPose(),
                RightHand = ReadPose()
            };
        }

        private BsorNoteEvent ReadNoteEvent()
        {
            var note = new BsorNoteEvent
            {
                NoteId = _reader.ReadInt32(),
                EventTime = _reader.ReadSingle(),
                SpawnTime = _reader.ReadSingle(),
                EventType = (BsorNoteEventType)_reader.ReadInt32()
            };

            if (note.EventType == BsorNoteEventType.Good || note.EventType == BsorNoteEventType.Bad)
            {
                note.CutInfo = new BsorNoteCutInfo
                {
                    SpeedOk = _reader.ReadBoolean(),
                    DirectionOk = _reader.ReadBoolean(),
                    SaberTypeOk = _reader.ReadBoolean(),
                    WasCutTooSoon = _reader.ReadBoolean(),
                    SaberSpeed = _reader.ReadSingle(),
                    SaberDirection = ReadVector3(),
                    SaberType = _reader.ReadInt32(),
                    TimeDeviation = _reader.ReadSingle(),
                    CutDirectionDeviation = _reader.ReadSingle(),
                    CutPoint = ReadVector3(),
                    CutNormal = ReadVector3(),
                    CutDistanceToCenter = _reader.ReadSingle(),
                    CutAngle = _reader.ReadSingle(),
                    BeforeCutRating = _reader.ReadSingle(),
                    AfterCutRating = _reader.ReadSingle()
                };
            }
            return note;
        }

        private BsorWallEvent ReadWallEvent()
        {
            return new BsorWallEvent
            {
                WallId = _reader.ReadInt32(),
                Energy = _reader.ReadSingle(),
                Time = _reader.ReadSingle(),
                SpawnTime = _reader.ReadSingle()
            };
        }

        private BsorHeightEvent ReadHeightEvent()
        {
            return new BsorHeightEvent { Height = _reader.ReadSingle(), Time = _reader.ReadSingle() };
        }

        private BsorPauseEvent ReadPauseEvent()
        {
            return new BsorPauseEvent { DurationSeconds = _reader.ReadInt64(), Time = _reader.ReadSingle() };
        }

        private Dictionary<string, byte[]> ReadCustomData()
        {
            var customData = new Dictionary<string, byte[]>();
            var count = _reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var key = ReadString();
                var length = _reader.ReadInt32();
                customData[key] = _reader.ReadBytes(length);
            }
            return customData;
        }

        private List<T> ReadList<T>(Func<T> readItem)
        {
            var count = _reader.ReadInt32();
            if (count < 0) throw new InvalidDataException("Negative BSOR list length");

            var items = new List<T>(Math.Min(count, 1 << 16));
            for (var i = 0; i < count; i++) items.Add(readItem());
            return items;
        }

        private TarfPose ReadPose()
        {
            return new TarfPose { Position = ReadVector3(), Rotation = ReadQuaternion() };
        }

        private TarfVector3 ReadVector3()
        {
            return new TarfVector3(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        }

        private TarfQuaternion ReadQuaternion()
        {
            return new TarfQuaternion(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        }

        private string ReadString()
        {
            var length = _reader.ReadInt32();
            if (length < 0) throw new InvalidDataException("Negative BSOR string length");
            return Encoding.UTF8.GetString(_reader.ReadBytes(length));
        }

        // Old BeatLeader versions wrote the character count of the player name
        // instead of its byte count, which is too short for non ASCII names. Like
        // the BeatLeader decoder, keep reading until the next value looks like the
        // length of the platform string ("steam", "oculus" or "oculuspc").
        private string ReadPlayerName()
        {
            var length = _reader.ReadInt32();
            if (length < 0) throw new InvalidDataException("Negative BSOR string length");

            var stream = _reader.BaseStream;
            var nameStart = stream.Position;
            var extraBytes = 0;
            if (length > 0)
            {
                while (true)
                {
                    stream.Position = nameStart + length + extraBytes;
                    var nextValue = _reader.ReadInt32();
                    if (nextValue == 5 || nextValue == 6 || nextValue == 8) break;
                    extraBytes++;
                }
            }

            stream.Position = nameStart;
            return Encoding.UTF8.GetString(_reader.ReadBytes(length + extraBytes));
        }
    }
}
