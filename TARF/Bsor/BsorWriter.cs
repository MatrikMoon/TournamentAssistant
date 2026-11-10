using System.Text;
using TournamentAssistantShared.Tarf;

namespace TARF.Bsor
{
    // Mirrors ReplayEncoder from the BeatLeader PC mod.
    public class BsorWriter
    {
        private readonly BinaryWriter _writer;

        private BsorWriter(Stream output)
        {
            _writer = new BinaryWriter(output, Encoding.UTF8);
        }

        public static byte[] Write(BsorReplay replay)
        {
            using var output = new MemoryStream();
            new BsorWriter(output).WriteReplay(replay);
            return output.ToArray();
        }

        private void WriteReplay(BsorReplay replay)
        {
            _writer.Write(BsorReplay.Magic);
            _writer.Write(BsorReplay.Version);

            _writer.Write((byte)BsorSection.Info);
            WriteInfo(replay.Info);

            _writer.Write((byte)BsorSection.Frames);
            WriteList(replay.Frames, WriteFrame);

            _writer.Write((byte)BsorSection.Notes);
            WriteList(replay.Notes, WriteNoteEvent);

            _writer.Write((byte)BsorSection.Walls);
            WriteList(replay.Walls, WriteWallEvent);

            _writer.Write((byte)BsorSection.Heights);
            WriteList(replay.Heights, height =>
            {
                _writer.Write(height.Height);
                _writer.Write(height.Time);
            });

            _writer.Write((byte)BsorSection.Pauses);
            WriteList(replay.Pauses, pause =>
            {
                _writer.Write(pause.DurationSeconds);
                _writer.Write(pause.Time);
            });

            var saberOffsets = replay.SaberOffsets ?? new BsorSaberOffsets
            {
                Left = new TarfPose { Rotation = TarfQuaternion.Identity },
                Right = new TarfPose { Rotation = TarfQuaternion.Identity }
            };
            _writer.Write((byte)BsorSection.SaberOffsets);
            WritePose(saberOffsets.Left);
            WritePose(saberOffsets.Right);

            _writer.Write((byte)BsorSection.CustomData);
            _writer.Write(replay.CustomData.Count);
            foreach (var entry in replay.CustomData)
            {
                WriteString(entry.Key);
                _writer.Write(entry.Value.Length);
                _writer.Write(entry.Value);
            }

            _writer.Flush();
        }

        private void WriteInfo(BsorInfo info)
        {
            WriteString(info.ModVersion);
            WriteString(info.GameVersion);
            WriteString(info.Timestamp);

            WriteString(info.PlayerId);
            WriteString(info.PlayerName);
            WriteString(info.Platform);

            WriteString(info.TrackingSystem);
            WriteString(info.Hmd);
            WriteString(info.Controller);

            WriteString(info.Hash);
            WriteString(info.SongName);
            WriteString(info.Mapper);
            WriteString(info.Difficulty);

            _writer.Write(info.Score);
            WriteString(info.Mode);
            WriteString(info.Environment);
            WriteString(info.Modifiers);
            _writer.Write(info.JumpDistance);
            _writer.Write(info.LeftHanded);
            _writer.Write(info.Height);

            _writer.Write(info.StartTime);
            _writer.Write(info.FailTime);
            _writer.Write(info.Speed);
        }

        private void WriteFrame(BsorFrame frame)
        {
            _writer.Write(frame.Time);
            _writer.Write(frame.Fps);
            WritePose(frame.Head);
            WritePose(frame.LeftHand);
            WritePose(frame.RightHand);
        }

        private void WriteNoteEvent(BsorNoteEvent note)
        {
            _writer.Write(note.NoteId);
            _writer.Write(note.EventTime);
            _writer.Write(note.SpawnTime);
            _writer.Write((int)note.EventType);

            if (note.EventType != BsorNoteEventType.Good && note.EventType != BsorNoteEventType.Bad) return;

            var cut = note.CutInfo;
            _writer.Write(cut.SpeedOk);
            _writer.Write(cut.DirectionOk);
            _writer.Write(cut.SaberTypeOk);
            _writer.Write(cut.WasCutTooSoon);
            _writer.Write(cut.SaberSpeed);
            WriteVector3(cut.SaberDirection);
            _writer.Write(cut.SaberType);
            _writer.Write(cut.TimeDeviation);
            _writer.Write(cut.CutDirectionDeviation);
            WriteVector3(cut.CutPoint);
            WriteVector3(cut.CutNormal);
            _writer.Write(cut.CutDistanceToCenter);
            _writer.Write(cut.CutAngle);
            _writer.Write(cut.BeforeCutRating);
            _writer.Write(cut.AfterCutRating);
        }

        private void WriteWallEvent(BsorWallEvent wall)
        {
            _writer.Write(wall.WallId);
            _writer.Write(wall.Energy);
            _writer.Write(wall.Time);
            _writer.Write(wall.SpawnTime);
        }

        private void WriteList<T>(List<T> items, Action<T> writeItem)
        {
            _writer.Write(items.Count);
            foreach (var item in items) writeItem(item);
        }

        private void WritePose(TarfPose pose)
        {
            WriteVector3(pose.Position);
            _writer.Write(pose.Rotation.X);
            _writer.Write(pose.Rotation.Y);
            _writer.Write(pose.Rotation.Z);
            _writer.Write(pose.Rotation.W);
        }

        private void WriteVector3(TarfVector3 vector)
        {
            _writer.Write(vector.X);
            _writer.Write(vector.Y);
            _writer.Write(vector.Z);
        }

        private void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            _writer.Write(bytes.Length);
            _writer.Write(bytes);
        }
    }
}
