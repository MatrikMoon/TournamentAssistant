using TournamentAssistantShared.Tarf;

namespace TARF.Bsor
{
    // The contents of a BSOR v1 file, as written by the BeatLeader mods.
    // See https://github.com/BeatLeader/BS-Open-Replay
    public class BsorReplay
    {
        public const int Magic = 0x442d3d69;
        public const byte Version = 1;

        public BsorInfo Info { get; set; } = new BsorInfo();
        public List<BsorFrame> Frames { get; set; } = new List<BsorFrame>();
        public List<BsorNoteEvent> Notes { get; set; } = new List<BsorNoteEvent>();
        public List<BsorWallEvent> Walls { get; set; } = new List<BsorWallEvent>();
        public List<BsorHeightEvent> Heights { get; set; } = new List<BsorHeightEvent>();
        public List<BsorPauseEvent> Pauses { get; set; } = new List<BsorPauseEvent>();

        // Older replays end before these two sections.
        public BsorSaberOffsets SaberOffsets { get; set; }
        public Dictionary<string, byte[]> CustomData { get; set; } = new Dictionary<string, byte[]>();
    }

    public enum BsorSection : byte
    {
        Info = 0,
        Frames = 1,
        Notes = 2,
        Walls = 3,
        Heights = 4,
        Pauses = 5,
        SaberOffsets = 6,
        CustomData = 7
    }

    public class BsorInfo
    {
        public string ModVersion = "";
        public string GameVersion = "";
        public string Timestamp = "";

        public string PlayerId = "";
        public string PlayerName = "";
        public string Platform = "";

        public string TrackingSystem = "";
        public string Hmd = "";
        public string Controller = "";

        public string Hash = "";
        public string SongName = "";
        public string Mapper = "";
        public string Difficulty = "";

        public int Score;
        public string Mode = "";
        public string Environment = "";
        public string Modifiers = "";
        public float JumpDistance;
        public bool LeftHanded;
        public float Height;

        public float StartTime;
        public float FailTime;
        public float Speed;
    }

    public class BsorFrame
    {
        public float Time;
        public int Fps;
        public TarfPose Head;
        public TarfPose LeftHand;
        public TarfPose RightHand;
    }

    public enum BsorNoteEventType
    {
        Good = 0,
        Bad = 1,
        Miss = 2,
        Bomb = 3
    }

    public class BsorNoteEvent
    {
        public int NoteId;
        public float EventTime;
        public float SpawnTime;
        public BsorNoteEventType EventType;

        // Only present for good and bad cuts.
        public BsorNoteCutInfo CutInfo;
    }

    public class BsorNoteCutInfo
    {
        public bool SpeedOk;
        public bool DirectionOk;
        public bool SaberTypeOk;
        public bool WasCutTooSoon;
        public float SaberSpeed;
        public TarfVector3 SaberDirection;
        public int SaberType;
        public float TimeDeviation;
        public float CutDirectionDeviation;
        public TarfVector3 CutPoint;
        public TarfVector3 CutNormal;
        public float CutDistanceToCenter;
        public float CutAngle;
        public float BeforeCutRating;
        public float AfterCutRating;
    }

    public class BsorWallEvent
    {
        public int WallId;
        public float Energy;
        public float Time;
        public float SpawnTime;
    }

    public class BsorHeightEvent
    {
        public float Height;
        public float Time;
    }

    public class BsorPauseEvent
    {
        public long DurationSeconds;
        public float Time;
    }

    public class BsorSaberOffsets
    {
        public TarfPose Left;
        public TarfPose Right;
    }
}
