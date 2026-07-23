using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Models.Replay;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.ReplayStream)]
    public class ReplayStream : ControllerBase
    {
        private const int MaxEventsPerChunk = 256;
        private const int MaxStringLength = 512;
        private const int MaxStartExtensionBytes = 32 * 1024;

        public TAServer TAServer { get; set; }

        [AllowFromPlayer]
        [PacketHandler]
        public async Task ForwardReplayStream([FromBody] Packet packet, [FromUser] User user)
        {
            var replay = packet.ReplayStream;
            var platformId = user?.PlatformId?.Trim();
            if (string.IsNullOrEmpty(platformId) || !IsValid(replay))
                return;

            // Sender identity is authoritative; clients cannot publish under another platform ID.
            replay.PlayerId = platformId;
            if (replay.Start != null)
            {
                replay.Start.ServerStartTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (replay.Start.Player != null)
                    replay.Start.Player.PlayerId = platformId;
            }
            if (replay.Chunk?.Cursor != null)
                replay.Chunk.Cursor.ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (replay.End?.Cursor != null)
                replay.End.Cursor.ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await TAServer.BroadcastReplayStream(Guid.Parse(user.Guid), platformId, user.Name, replay);
        }

        private static bool IsValid(ReplayStreamPacket packet)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.StreamId) || packet.StreamId.Length > 128
                || !Valid(packet.ConnectionId) || !Valid(packet.PlayerId) || !Valid(packet.MatchId))
                return false;
            if (packet.Start != null)
            {
                var start = packet.Start;
                return start.ProtocolVersion == 1
                    && start.Player != null && start.Beatmap != null && start.ReplayMetadata != null
                    && !string.IsNullOrWhiteSpace(start.Beatmap.LevelId)
                    && !string.IsNullOrWhiteSpace(start.Beatmap.Characteristic)
                    && Valid(start.GameSessionId)
                    && Valid(start.Player.GameVersion)
                    && Valid(start.Player.ClientVersion)
                    && Valid(start.Beatmap.MapHash)
                    && Valid(start.Beatmap.LevelId)
                    && Valid(start.Beatmap.DifficultyName)
                    && Valid(start.Beatmap.Characteristic)
                    && Valid(start.ReplayMetadata.ReplayVersion)
                    && Valid(start.ReplayMetadata.LevelId)
                    && Valid(start.ReplayMetadata.Characteristic)
                    && Valid(start.ReplayMetadata.Environment)
                    && Valid(start.ReplayMetadata.GameVersion)
                    && Valid(start.ReplayMetadata.PluginVersion)
                    && Valid(start.ReplayMetadata.Platform)
                    && Valid(start.ReplayMetadata.EnvironmentOverride)
                    && Valid(start.ReplayMetadata.ColorSchemeId)
                    && (start.Beatmap.Modifiers?.Count ?? 0) <= 64
                    && (start.ReplayMetadata.Modifiers?.Count ?? 0) <= 64
                    && (start.Beatmap.Modifiers?.All(Valid) ?? true)
                    && (start.ReplayMetadata.Modifiers?.All(Valid) ?? true)
                    && Finite(start.ReplayMetadata.JumpDistance)
                    && Finite(start.ReplayMetadata.InitialHeight)
                    && Finite(start.ReplayMetadata.NoteSpawnOffset)
                    && Finite(start.ReplayMetadata.RoomRotation)
                    && Finite(start.ReplayMetadata.SongSpeed)
                    && ValidVector(start.ReplayMetadata.RoomCenter)
                    && ValidColor(start.ReplayMetadata.LeftSaberColor)
                    && ValidColor(start.ReplayMetadata.RightSaberColor)
                    && start.ReplayExtensions.Count <= 8
                    && start.ReplayExtensions.All(x => x != null && Valid(x.Id) && x.Version > 0
                        && (x.Payload?.Length ?? 0) <= MaxStartExtensionBytes);
            }
            if (packet.Chunk != null)
            {
                var events = packet.Chunk.Events;
                if (packet.Chunk.Cursor == null || events == null || !Finite(events.MinTimeSeconds) || !Finite(events.MaxTimeSeconds))
                    return false;
                var count = events.PoseFrames.Count + events.HeightEvents.Count + events.NoteEvents.Count
                    + events.ScoreEvents.Count + events.ComboEvents.Count + events.MultiplierEvents.Count
                    + events.EnergyEvents.Count + events.PauseEvents.Count;
                return count > 0 && count <= MaxEventsPerChunk
                    && events.MinTimeSeconds <= events.MaxTimeSeconds
                    && events.PoseFrames.All(x => Finite(x.TimeSeconds) && ValidPose(x.Head) && ValidPose(x.Left) && ValidPose(x.Right))
                    && events.HeightEvents.All(x => Finite(x.Height) && Finite(x.TimeSeconds))
                    && events.ScoreEvents.All(x => Finite(x.TimeSeconds))
                    && events.ComboEvents.All(x => Finite(x.TimeSeconds))
                    && events.MultiplierEvents.All(x => Finite(x.NextMultiplierProgress) && Finite(x.TimeSeconds))
                    && events.EnergyEvents.All(x => Finite(x.Energy) && Finite(x.TimeSeconds))
                    && events.PauseEvents.All(x => Finite(x.TimeSeconds))
                    && events.NoteEvents.All(x => Finite(x.TimeSeconds) && Finite(x.SaberSpeed) && Finite(x.CutAngle)
                        && Finite(x.CutDistanceToCenter) && Finite(x.CutDirectionDeviation)
                        && Finite(x.BeforeCutRating) && Finite(x.AfterCutRating) && Finite(x.UnityTimescale)
                        && Finite(x.TimeSyncTimescale) && Finite(x.TimeDeviation)
                        && ValidVector(x.CutPoint) && ValidVector(x.CutNormal) && ValidVector(x.SaberDirection)
                        && ValidVector(x.NotePosition) && ValidQuaternion(x.WorldRotation)
                        && ValidQuaternion(x.InverseWorldRotation) && ValidQuaternion(x.NoteRotation));
            }
            return packet.End?.Cursor != null;
        }

        private static bool Valid(string value) => value == null || value.Length <= MaxStringLength;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidPose(ReplayPose pose) => pose != null && pose.Position != null && pose.Rotation != null
            && ValidVector(pose.Position) && ValidQuaternion(pose.Rotation);
        private static bool ValidVector(ReplayVector3 vector) => vector == null
            || (Finite(vector.X) && Finite(vector.Y) && Finite(vector.Z));
        private static bool ValidQuaternion(ReplayQuaternion quaternion) => quaternion == null
            || (Finite(quaternion.X) && Finite(quaternion.Y) && Finite(quaternion.Z) && Finite(quaternion.W));
        private static bool ValidColor(ReplayColor color) => color == null ||
            (Finite(color.R) && Finite(color.G) && Finite(color.B) && Finite(color.A)
             && color.R >= 0 && color.R <= 1 && color.G >= 0 && color.G <= 1
             && color.B >= 0 && color.B <= 1 && color.A >= 0 && color.A <= 1);
    }
}
