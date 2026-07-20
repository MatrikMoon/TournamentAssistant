using System.Collections.Generic;
using TournamentAssistantShared.Models.Replay;

namespace TournamentAssistantServer.Models
{
    public sealed class LiveReplayStatus
    {
        public string PlatformId { get; set; }
        public bool IsInMatch { get; set; }
        public bool IsInQualifier { get; set; }
        public int ViewCount { get; set; }

        public string MatchGuid { get; set; }

        public string StreamId { get; set; }
        public string ConnectionId { get; set; }
        public string PlayerName { get; set; }
        public uint ProtocolVersion { get; set; }
        public long ClientStartTimeUnixMs { get; set; }
        public long ServerStartTimeUnixMs { get; set; }
        public string GameSessionId { get; set; }
        public LiveReplayPlayer Player { get; set; }
        public LiveReplayBeatmap Beatmap { get; set; }
        public LiveReplayMetadata ReplayMetadata { get; set; }
    }

    public sealed class LiveReplayPlayer
    {
        public string PlayerId { get; set; }
        public ReplayPlatform Platform { get; set; }
        public string GameVersion { get; set; }
        public string ClientVersion { get; set; }
    }

    public sealed class LiveReplayBeatmap
    {
        public string MapHash { get; set; }
        public string LevelId { get; set; }
        public int Difficulty { get; set; }
        public string DifficultyName { get; set; }
        public string Characteristic { get; set; }
        public List<string> Modifiers { get; set; }
        public uint MaxScore { get; set; }
    }

    public sealed class LiveReplayMetadata
    {
        public string ReplayVersion { get; set; }
        public string LevelId { get; set; }
        public int Difficulty { get; set; }
        public string Characteristic { get; set; }
        public string Environment { get; set; }
        public List<string> Modifiers { get; set; }
        public float NoteSpawnOffset { get; set; }
        public bool LeftHanded { get; set; }
        public float InitialHeight { get; set; }
        public float RoomRotation { get; set; }
        public LiveReplayVector3 RoomCenter { get; set; }
        public string GameVersion { get; set; }
        public string PluginVersion { get; set; }
        public string Platform { get; set; }
        public float SongSpeed { get; set; }
        public float JumpDistance { get; set; }
        public LiveReplayColor LeftSaberColor { get; set; }
        public LiveReplayColor RightSaberColor { get; set; }
        public string EnvironmentOverride { get; set; }
        public string ColorSchemeId { get; set; }
    }

    public sealed class LiveReplayVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class LiveReplayColor
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
        public float A { get; set; }
    }
}
