using System.Text;
using LzmaDecoder = SevenZip.Compression.LZMA.Decoder;
using TournamentAssistantShared.Tarf;

namespace TARF.ScoreSaber
{
    public class ScoreSaberReader
    {
        private static readonly byte[] FileHeader = Encoding.UTF8.GetBytes("ScoreSaber Replay 👌🤠\r\n");
        private static readonly Version EventsVersion2 = new Version(2, 0, 0);
        private static readonly Version MetadataVersion3 = new Version(3, 1, 0);
        private static readonly Version NewestSupportedVersion = new Version(3, 1, 0);

        private const int ExtensionMagic = 0x31585353;
        private const int ExtensionTableVersion = 1;
        private const string PlaySettingsExtension = "scoresaber.play-settings";
        private const string PauseEventsExtension = "scoresaber.pause-events";
        private const string WallEventsExtension = "scoresaber.wall-events";
        private const string ControllerOffsetsExtension = "scoresaber.controller-offsets";

        private readonly BinaryReader _reader;

        private ScoreSaberReader(byte[] decompressed)
        {
            _reader = new BinaryReader(new MemoryStream(decompressed), Encoding.UTF8);
        }

        public static bool IsScoreSaberReplay(byte[] data)
        {
            return data.Length > FileHeader.Length && data.AsSpan(0, FileHeader.Length).SequenceEqual(FileHeader);
        }

        public static ScoreSaberReplay Read(byte[] data)
        {
            if (!IsScoreSaberReplay(data)) throw new InvalidDataException("Not a ScoreSaber replay");
            return new ScoreSaberReader(Decompress(data.AsSpan(FileHeader.Length).ToArray())).ReadReplay();
        }

        private static byte[] Decompress(byte[] compressed)
        {
            const int propertiesLength = 5;
            const int sizeLength = 8;

            var properties = compressed.AsSpan(0, propertiesLength).ToArray();
            var decompressedSize = BitConverter.ToInt64(compressed, propertiesLength);
            var dataStart = propertiesLength + sizeLength;

            var decoder = new LzmaDecoder();
            decoder.SetDecoderProperties(properties);

            using var input = new MemoryStream(compressed, dataStart, compressed.Length - dataStart);
            using var output = new MemoryStream();
            decoder.Code(input, output, input.Length, decompressedSize, null);
            return output.ToArray();
        }

        private ScoreSaberReplay ReadReplay()
        {
            var metadataOffset = _reader.ReadInt32();
            var poseOffset = _reader.ReadInt32();
            var heightOffset = _reader.ReadInt32();
            var noteOffset = _reader.ReadInt32();
            var scoreOffset = _reader.ReadInt32();
            var comboOffset = _reader.ReadInt32();
            var multiplierOffset = _reader.ReadInt32();
            var energyOffset = _reader.ReadInt32();
            var extensionsOffset = _reader.ReadInt32();

            Seek(metadataOffset);
            var metadata = ReadMetadata();
            var version = new Version(metadata.Version);
            if (version > NewestSupportedVersion) throw new NotSupportedException($"ScoreSaber replay version {version} is not supported");

            var usesVersion2Events = version == EventsVersion2;
            var replay = new ScoreSaberReplay { Metadata = metadata };

            Seek(poseOffset);
            replay.PoseFrames = ReadList(ReadPoseFrame);
            Seek(heightOffset);
            replay.HeightEvents = ReadList(() => new ScoreSaberHeightEvent { Height = _reader.ReadSingle(), Time = _reader.ReadSingle() });
            Seek(noteOffset);
            replay.NoteEvents = ReadList(() => ReadNoteEvent(usesVersion2Events));
            Seek(scoreOffset);
            replay.ScoreEvents = ReadList(() => ReadScoreEvent(usesVersion2Events));
            Seek(comboOffset);
            replay.ComboEvents = ReadList(() => new ScoreSaberComboEvent { Combo = _reader.ReadInt32(), Time = _reader.ReadSingle() });
            Seek(multiplierOffset);
            replay.MultiplierEvents = ReadList(() => new ScoreSaberMultiplierEvent
            {
                Multiplier = _reader.ReadInt32(),
                NextMultiplierProgress = _reader.ReadSingle(),
                Time = _reader.ReadSingle()
            });
            Seek(energyOffset);
            replay.EnergyEvents = ReadList(() => new ScoreSaberEnergyEvent { Energy = _reader.ReadSingle(), Time = _reader.ReadSingle() });

            ReadExtensions(replay, extensionsOffset);
            return replay;
        }

        private ScoreSaberMetadata ReadMetadata()
        {
            var metadata = new ScoreSaberMetadata
            {
                Version = ReadString(),
                LevelId = ReadString(),
                Difficulty = _reader.ReadInt32(),
                Characteristic = ReadString(),
                Environment = ReadString(),
                Modifiers = ReadList(ReadString).ToArray(),
                NoteSpawnOffset = _reader.ReadSingle(),
                LeftHanded = _reader.ReadBoolean(),
                InitialHeight = _reader.ReadSingle(),
                RoomRotation = _reader.ReadSingle(),
                RoomCenter = ReadVector3(),
                FailTime = _reader.ReadSingle()
            };

            if (new Version(metadata.Version) >= MetadataVersion3)
            {
                metadata.GameVersion = ReadString();
                metadata.PluginVersion = ReadString();
                metadata.Platform = ReadString();
            }
            return metadata;
        }

        private void ReadExtensions(ScoreSaberReplay replay, int offset)
        {
            var length = _reader.BaseStream.Length;
            if (offset <= 0 || offset + 12 > length) return;

            Seek(offset);
            if (_reader.ReadInt32() != ExtensionMagic || _reader.ReadInt32() != ExtensionTableVersion) return;

            var entryCount = _reader.ReadInt32();
            for (var i = 0; i < entryCount; i++)
            {
                var id = ReadString();
                var version = _reader.ReadInt32();
                var payloadLength = _reader.ReadInt32();
                var payloadStart = _reader.BaseStream.Position;
                if (payloadLength < 0 || payloadStart + payloadLength > length)
                {
                    throw new InvalidDataException("ScoreSaber replay extension is out of bounds");
                }

                if (id == PlaySettingsExtension && version == 1) replay.PlaySettings = ReadPlaySettings();
                else if (id == PauseEventsExtension && version == 1) replay.PauseEvents = ReadList(ReadPauseEvent);
                else if (id == WallEventsExtension && version == 1) replay.WallEvents = ReadList(ReadWallEvent);
                else if (id == ControllerOffsetsExtension && version == 1) replay.ControllerOffsets = ReadControllerOffsets();
                else replay.OtherExtensions.Add(new ScoreSaberExtension { Id = id, Version = version, Payload = _reader.ReadBytes(payloadLength) });

                Seek(payloadStart + payloadLength);
            }
        }

        private ScoreSaberPlaySettings ReadPlaySettings()
        {
            return new ScoreSaberPlaySettings
            {
                SongSpeed = _reader.ReadSingle(),
                JumpDistance = _reader.ReadSingle(),
                LeftSaberColor = ReadOptionalColor(),
                RightSaberColor = ReadOptionalColor(),
                ObstacleColor = ReadOptionalColor(),
                EnvironmentColor0 = ReadOptionalColor(),
                EnvironmentColor1 = ReadOptionalColor(),
                EnvironmentColorW = ReadOptionalColor(),
                EnvironmentColor0Boost = ReadOptionalColor(),
                EnvironmentColor1Boost = ReadOptionalColor(),
                EnvironmentColorWBoost = ReadOptionalColor(),
                SupportsEnvironmentColorBoost = _reader.ReadBoolean(),
                Environment = ReadString(),
                EnvironmentEffectsFilterDefaultPreset = _reader.ReadInt32(),
                EnvironmentEffectsFilterExpertPlusPreset = _reader.ReadInt32(),
                EnvironmentEffectsFilterPreset = _reader.ReadInt32(),
                NoTextsAndHuds = _reader.ReadBoolean(),
                SaberTrailIntensity = _reader.ReadSingle(),
                HideNoteSpawnEffect = _reader.ReadBoolean(),
                ArcsHapticFeedback = _reader.ReadBoolean(),
                ArcVisibility = _reader.ReadInt32()
            };
        }

        private ScoreSaberControllerOffsets ReadControllerOffsets()
        {
            return new ScoreSaberControllerOffsets
            {
                Shared = ReadOptionalControllerOffset(),
                Left = ReadOptionalControllerOffset(),
                Right = ReadOptionalControllerOffset()
            };
        }

        private ScoreSaberControllerOffset ReadOptionalControllerOffset()
        {
            if (!_reader.ReadBoolean()) return null;
            return new ScoreSaberControllerOffset { Position = ReadVector3(), EulerRotation = ReadVector3() };
        }

        private ScoreSaberPoseFrame ReadPoseFrame()
        {
            return new ScoreSaberPoseFrame
            {
                Head = ReadPose(),
                Left = ReadPose(),
                Right = ReadPose(),
                Fps = _reader.ReadInt32(),
                Time = _reader.ReadSingle()
            };
        }

        private ScoreSaberNoteEvent ReadNoteEvent(bool usesVersion2Events)
        {
            var noteId = new ScoreSaberNoteId
            {
                Time = _reader.ReadSingle(),
                LineLayer = _reader.ReadInt32(),
                LineIndex = _reader.ReadInt32(),
                ColorType = _reader.ReadInt32(),
                CutDirection = _reader.ReadInt32()
            };
            if (!usesVersion2Events)
            {
                noteId.GameplayType = _reader.ReadInt32();
                noteId.ScoringType = _reader.ReadInt32();
                noteId.CutDirectionAngleOffset = _reader.ReadSingle();
            }

            var note = new ScoreSaberNoteEvent
            {
                NoteId = noteId,
                EventType = (ScoreSaberNoteEventType)_reader.ReadInt32(),
                CutPoint = ReadVector3(),
                CutNormal = ReadVector3(),
                SaberDirection = ReadVector3(),
                SaberType = _reader.ReadInt32(),
                DirectionOk = _reader.ReadBoolean(),
                SaberSpeed = _reader.ReadSingle(),
                CutAngle = _reader.ReadSingle(),
                CutDistanceToCenter = _reader.ReadSingle(),
                CutDirectionDeviation = _reader.ReadSingle(),
                BeforeCutRating = _reader.ReadSingle(),
                AfterCutRating = _reader.ReadSingle(),
                Time = _reader.ReadSingle(),
                UnityTimescale = _reader.ReadSingle(),
                TimeSyncTimescale = _reader.ReadSingle()
            };
            if (!usesVersion2Events)
            {
                note.TimeDeviation = _reader.ReadSingle();
                note.WorldRotation = ReadQuaternion();
                note.InverseWorldRotation = ReadQuaternion();
                note.NoteRotation = ReadQuaternion();
                note.NotePosition = ReadVector3();
            }
            return note;
        }

        private ScoreSaberScoreEvent ReadScoreEvent(bool usesVersion2Events)
        {
            var score = new ScoreSaberScoreEvent { Score = _reader.ReadInt32(), Time = _reader.ReadSingle() };
            if (!usesVersion2Events) score.ImmediateMaxPossibleScore = _reader.ReadInt32();
            return score;
        }

        private ScoreSaberPauseEvent ReadPauseEvent()
        {
            return new ScoreSaberPauseEvent
            {
                Time = _reader.ReadSingle(),
                DurationMs = _reader.ReadInt64(),
                UnixStartTimeMs = _reader.ReadInt64(),
                UnixEndTimeMs = _reader.ReadInt64()
            };
        }

        private ScoreSaberWallEvent ReadWallEvent()
        {
            return new ScoreSaberWallEvent
            {
                Time = _reader.ReadSingle(),
                ExitTime = _reader.ReadSingle(),
                Energy = _reader.ReadSingle(),
                ObstacleTime = _reader.ReadSingle(),
                ObstacleDuration = _reader.ReadSingle(),
                LineIndex = _reader.ReadInt32(),
                LineLayer = _reader.ReadInt32(),
                Width = _reader.ReadInt32(),
                Height = _reader.ReadInt32()
            };
        }

        private List<T> ReadList<T>(Func<T> readItem)
        {
            var count = _reader.ReadInt32();
            if (count < 0) throw new InvalidDataException("Negative ScoreSaber list length");

            var items = new List<T>(Math.Min(count, 1 << 16));
            for (var i = 0; i < count; i++) items.Add(readItem());
            return items;
        }

        private System.Numerics.Vector4? ReadOptionalColor()
        {
            if (!_reader.ReadBoolean()) return null;
            return new System.Numerics.Vector4(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
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
            if (length < 0) throw new InvalidDataException("Negative ScoreSaber string length");
            return Encoding.UTF8.GetString(_reader.ReadBytes(length));
        }

        private void Seek(long offset)
        {
            _reader.BaseStream.Position = offset;
        }
    }
}
