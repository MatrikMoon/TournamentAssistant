using TournamentAssistantShared.Tarf;

namespace TARF.ScoreSaber
{
    public class ScoreSaberReplay
    {
        public ScoreSaberMetadata Metadata { get; set; } = new ScoreSaberMetadata();
        public List<ScoreSaberPoseFrame> PoseFrames { get; set; } = new List<ScoreSaberPoseFrame>();
        public List<ScoreSaberHeightEvent> HeightEvents { get; set; } = new List<ScoreSaberHeightEvent>();
        public List<ScoreSaberNoteEvent> NoteEvents { get; set; } = new List<ScoreSaberNoteEvent>();
        public List<ScoreSaberScoreEvent> ScoreEvents { get; set; } = new List<ScoreSaberScoreEvent>();
        public List<ScoreSaberComboEvent> ComboEvents { get; set; } = new List<ScoreSaberComboEvent>();
        public List<ScoreSaberMultiplierEvent> MultiplierEvents { get; set; } = new List<ScoreSaberMultiplierEvent>();
        public List<ScoreSaberEnergyEvent> EnergyEvents { get; set; } = new List<ScoreSaberEnergyEvent>();

        public List<ScoreSaberPauseEvent> PauseEvents { get; set; } = new List<ScoreSaberPauseEvent>();
        public List<ScoreSaberWallEvent> WallEvents { get; set; } = new List<ScoreSaberWallEvent>();
        public ScoreSaberPlaySettings PlaySettings { get; set; }
        public ScoreSaberControllerOffsets ControllerOffsets { get; set; }

        public List<ScoreSaberExtension> OtherExtensions { get; set; } = new List<ScoreSaberExtension>();
    }

    public class ScoreSaberMetadata
    {
        public string Version = "";
        public string LevelId = "";
        public int Difficulty;
        public string Characteristic = "";
        public string Environment = "";
        public string[] Modifiers = Array.Empty<string>();
        public float NoteSpawnOffset;
        public bool LeftHanded;
        public float InitialHeight;
        public float RoomRotation;
        public TarfVector3 RoomCenter;
        public float FailTime;

        public string GameVersion = "";
        public string PluginVersion = "";
        public string Platform = "";
    }

    public class ScoreSaberPlaySettings
    {
        public float SongSpeed;
        public float JumpDistance;
        public System.Numerics.Vector4? LeftSaberColor;
        public System.Numerics.Vector4? RightSaberColor;
        public System.Numerics.Vector4? ObstacleColor;
        public System.Numerics.Vector4? EnvironmentColor0;
        public System.Numerics.Vector4? EnvironmentColor1;
        public System.Numerics.Vector4? EnvironmentColorW;
        public System.Numerics.Vector4? EnvironmentColor0Boost;
        public System.Numerics.Vector4? EnvironmentColor1Boost;
        public System.Numerics.Vector4? EnvironmentColorWBoost;
        public bool SupportsEnvironmentColorBoost;
        public string Environment = "";
        public int EnvironmentEffectsFilterDefaultPreset;
        public int EnvironmentEffectsFilterExpertPlusPreset;
        public int EnvironmentEffectsFilterPreset;
        public bool NoTextsAndHuds;
        public float SaberTrailIntensity;
        public bool HideNoteSpawnEffect;
        public bool ArcsHapticFeedback;
        public int ArcVisibility;
    }

    public class ScoreSaberControllerOffsets
    {
        public ScoreSaberControllerOffset Shared;
        public ScoreSaberControllerOffset Left;
        public ScoreSaberControllerOffset Right;
    }

    public class ScoreSaberControllerOffset
    {
        public TarfVector3 Position;
        public TarfVector3 EulerRotation;
    }

    public class ScoreSaberExtension
    {
        public string Id = "";
        public int Version;
        public byte[] Payload = Array.Empty<byte>();
    }

    public class ScoreSaberPoseFrame
    {
        public TarfPose Head;
        public TarfPose Left;
        public TarfPose Right;
        public int Fps;
        public float Time;
    }

    public enum ScoreSaberNoteEventType
    {
        None = 0,
        GoodCut = 1,
        BadCut = 2,
        Miss = 3,
        Bomb = 4
    }

    public class ScoreSaberNoteId
    {
        public float Time;
        public int LineLayer;
        public int LineIndex;
        public int ColorType;
        public int CutDirection;

        // Only written from replay version 3 onwards.
        public int? GameplayType;
        public int? ScoringType;
        public float? CutDirectionAngleOffset;
    }

    public class ScoreSaberNoteEvent
    {
        public ScoreSaberNoteId NoteId;
        public ScoreSaberNoteEventType EventType;
        public TarfVector3 CutPoint;
        public TarfVector3 CutNormal;
        public TarfVector3 SaberDirection;
        public int SaberType;
        public bool DirectionOk;
        public float SaberSpeed;
        public float CutAngle;
        public float CutDistanceToCenter;
        public float CutDirectionDeviation;
        public float BeforeCutRating;
        public float AfterCutRating;
        public float Time;
        public float UnityTimescale;
        public float TimeSyncTimescale;

        // Only written from replay version 3 onwards
        public float? TimeDeviation;
        public TarfQuaternion? WorldRotation;
        public TarfQuaternion? InverseWorldRotation;
        public TarfQuaternion? NoteRotation;
        public TarfVector3? NotePosition;
    }

    public class ScoreSaberHeightEvent
    {
        public float Height;
        public float Time;
    }

    public class ScoreSaberScoreEvent
    {
        public int Score;
        public float Time;
        public int? ImmediateMaxPossibleScore;
    }

    public class ScoreSaberComboEvent
    {
        public int Combo;
        public float Time;
    }

    public class ScoreSaberMultiplierEvent
    {
        public int Multiplier;
        public float NextMultiplierProgress;
        public float Time;
    }

    public class ScoreSaberEnergyEvent
    {
        public float Energy;
        public float Time;
    }

    public class ScoreSaberPauseEvent
    {
        public float Time;
        public long DurationMs;
        public long UnixStartTimeMs;
        public long UnixEndTimeMs;
    }

    public class ScoreSaberWallEvent
    {
        public float Time;
        public float ExitTime;
        public float Energy;
        public float ObstacleTime;
        public float ObstacleDuration;
        public int LineIndex;
        public int LineLayer;
        public int Width;
        public int Height;
    }
}
